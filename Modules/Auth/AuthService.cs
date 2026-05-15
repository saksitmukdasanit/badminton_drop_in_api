using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using DropInBadAPI.Modules.Auth;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;

namespace DropInBadAPI.Repositories
{
    public class AuthService : IAuthService
    {
        /// <summary>เก็บใน UserLogins.ProviderKey ขณะ <see cref="UseOtpBypass"/> — ไม่เรียก SMSMKT</summary>
        private const string OtpBypassMarker = "__OTP_BYPASS__";

        private readonly BadmintonDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IGoogleTokenVerifier _googleVerifier;
        private readonly IAppleTokenVerifier _appleVerifier;

        public AuthService(
            BadmintonDbContext context,
            IJwtService jwtService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IPasswordHasher passwordHasher,
            IGoogleTokenVerifier googleVerifier,
            IAppleTokenVerifier appleVerifier)
        {
            _context = context;
            _jwtService = jwtService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
            _googleVerifier = googleVerifier;
            _appleVerifier = appleVerifier;
        }

        private bool UseOtpBypass() =>
            string.Equals(_configuration["SmsMkt:BypassOtp"], "true", StringComparison.OrdinalIgnoreCase);

        public async Task<(string? AccessToken, string? RefreshToken, string ErrorMessage)> RegisterAsync(InitiateRegisterDto dto)
        {
            // 1. ตรวจสอบเบอร์โทรศัพท์ก่อน
            var existingProfile = await _context.UserProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(up => up.PhoneNumber == dto.PhoneNumber);

            if (existingProfile != null)
            {
                if (existingProfile.IsPhoneNumberVerified)
                {
                    return (null, null, "Phone number already exists.");
                }
                // ถ้ามีเบอร์แต่ยังไม่ยืนยัน (สมัครค้างไว้) ให้ลบข้อมูลเก่าทิ้งเพื่อสมัครใหม่
                _context.Users.Remove(existingProfile.User);
                await _context.SaveChangesAsync();
            }

            // 2. ตรวจสอบ Username (หลังจากเคลียร์ User เก่าที่ค้างอยู่แล้ว)
            if (await _context.UserLogins.AnyAsync(ul => ul.ProviderKey == dto.Username && ul.ProviderName == "Local"))
                return (null, null, "Username already exists.");

            var passwordHash = _passwordHasher.Hash(dto.Password);

            var newUser = new User { IsActive = true }; // Active ได้เลย
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _context.UserProfiles.Add(new UserProfile { UserId = newUser.UserId, PhoneNumber = dto.PhoneNumber, IsPhoneNumberVerified = false }); // ยังไม่ Verify

            var userLogin = new UserLogin { ProviderName = "Local", ProviderKey = dto.Username, PasswordHash = passwordHash, UserId = newUser.UserId };

            // สร้างและบันทึก Refresh Token
            var refreshToken = _jwtService.CreateRefreshToken();
            userLogin.RefreshToken = refreshToken;
            userLogin.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(90);
            _context.UserLogins.Add(userLogin);

            await _context.SaveChangesAsync();

            // สร้าง Access Token แล้วส่งกลับไป
            var accessToken = _jwtService.CreateAccessToken(newUser);

            // ส่ง OTP ทันทีเมื่อสมัครเสร็จ
            var (otpSuccess, otpMessage) = await ResendOtpAsync(dto.PhoneNumber);
            if (!otpSuccess)
            {
                // ถ้าส่ง OTP ไม่ผ่าน ให้แจ้ง Error กลับไปทันที
                // (User จะถูกลบอัตโนมัติเมื่อสมัครใหม่ในครั้งถัดไป ตาม Logic ที่เพิ่มไว้ก่อนหน้า)
                return (null, null, "สมัครสมาชิกสำเร็จ แต่ส่ง OTP ไม่ผ่าน: " + otpMessage);
            }

            return (accessToken, refreshToken, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> CompleteUserProfileAsync(int userId, CompleteProfileDto dto)
        {
            var userProfile = await _context.UserProfiles.FindAsync(userId);
            if (userProfile == null)
            {
                return (false, "User profile not found.");
            }

            // 1. ตรวจสอบว่า Email ซ้ำกับคนอื่นหรือไม่ (ยกเว้นตัวเอง)
            if (await _context.UserProfiles.AnyAsync(up => up.PrimaryContactEmail == dto.Email && up.UserId != userId))
            {
                return (false, "Email is already in use.");
            }

            userProfile.Nickname = dto.Nickname;
            userProfile.FirstName = dto.FirstName;
            userProfile.LastName = dto.LastName;
            userProfile.PrimaryContactEmail = dto.Email;
            userProfile.Gender = (byte)dto.Gender;
            userProfile.ProfilePhotoUrl = dto.ProfilePhotoUrl;
            userProfile.EmergencyContactName = dto.EmergencyContactName;
            userProfile.EmergencyContactPhone = dto.EmergencyContactPhone;
            userProfile.UpdatedDate = DateTime.UtcNow;
            userProfile.UpdatedBy = userId;

            await _context.SaveChangesAsync();
            return (true, string.Empty);
        }
        public async Task<(string? AccessToken, string? RefreshToken, string ErrorMessage)> LoginUserAsync(LoginDto loginDto)
        {
            var userLogin = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.ProviderKey == loginDto.Username && ul.ProviderName == "Local");

            if (userLogin == null) return (null, null, "Invalid username or password.");

            if (string.IsNullOrEmpty(userLogin.PasswordHash) ||
                !_passwordHasher.Verify(loginDto.Password, userLogin.PasswordHash))
            {
                return (null, null, "Invalid username or password.");
            }

            // Lazy upgrade: ถ้าเป็น legacy hash ให้ rehash เป็น BCrypt
            if (_passwordHasher.IsLegacyHash(userLogin.PasswordHash))
            {
                userLogin.PasswordHash = _passwordHasher.Hash(loginDto.Password);
            }

            var user = await _context.Users.FindAsync(userLogin.UserId);
            if (user == null) return (null, null, "User not found.");

            // 2. ตรวจสอบว่ายืนยันเบอร์โทรศัพท์หรือยัง
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.UserId);
            if (userProfile != null && !userProfile.IsPhoneNumberVerified)
            {
                return (null, null, "เบอร์โทรศัพท์นี้ยังไม่ผ่านการยืนยัน OTP กรุณากดสมัครสมาชิกใหม่อีกครั้ง (ข้อมูลเดิมที่ค้างอยู่จะถูกล้างอัตโนมัติ)");
            }

            // สร้าง Token ทั้ง 2 ตัว
            var accessToken = _jwtService.CreateAccessToken(user);
            var refreshToken = _jwtService.CreateRefreshToken();

            // บันทึก Refresh Token ลงฐานข้อมูล
            userLogin.RefreshToken = refreshToken;
            userLogin.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(90); // ตั้งวันหมดอายุ
            await _context.SaveChangesAsync();

            return (accessToken, refreshToken, string.Empty);
        }
        public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
        {
            // ดึงข้อมูลจาก UserProfiles และแปลงเป็น DTO เพื่อส่งกลับ
            var profile = await _context.UserProfiles
                .Where(p => p.UserId == userId)
                .Include(p => p.User.OrganizerProfile) 
                .Select(p => new UserProfileDto(
                    p.UserId,
                    p.ProfilePhotoUrl,
                    p.PrimaryContactEmail,
                    p.Nickname,
                    p.FirstName,
                    p.LastName,
                    p.Gender == 1 ? "ชาย" :
                    p.Gender == 2 ? "หญิง" :
                    p.Gender == 3 ? "อื่นๆ" : null,
                    p.PhoneNumber,
                    p.IsPhoneNumberVerified,
                    p.EmergencyContactName,
                    p.EmergencyContactPhone,
                    p.User.OrganizerProfile == null ? null : p.User.OrganizerProfile != null && p.User.OrganizerProfile.Status == 1
                    )).FirstOrDefaultAsync();

            return profile;
        }

        public async Task<(string? AccessToken, string? RefreshToken)> RefreshTokenAsync(string accessToken, string refreshToken)
        {
            // 1. พยายามแกะ UserID จาก Access Token (ถ้าทำได้)
            int? userIdFromAccessToken = null;
            try 
            {
                var principal = _jwtService.GetPrincipalFromExpiredToken(accessToken);
                var claimValue = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (claimValue != null) userIdFromAccessToken = int.Parse(claimValue);
            }
            catch
            {
                // FIX: ถ้าแกะ Access Token ไม่ได้ (เช่น เสียรูปแบบ) ให้ปล่อยผ่านไปก่อน 
                // แล้วไปตัดสินจาก Refresh Token ในฐานข้อมูลแทน
            }

            // 2. ค้นหา UserLogin จาก Refresh Token โดยตรง
            var userLogin = await _context.UserLogins.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

            // 3. ตรวจสอบความถูกต้องของ Refresh Token
            if (userLogin == null || userLogin.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return (null, null); // ไม่เจอ หรือ หมดอายุ -> จบ
            }

            // 4. (Optional) ถ้าแกะ Access Token ได้ ต้องตรวจสอบว่าตรงกัน
            // แต่ถ้าแกะไม่ได้ (userIdFromAccessToken == null) เราจะเชื่อ Refresh Token ไปเลย
            if (userIdFromAccessToken.HasValue && userLogin.UserId != userIdFromAccessToken.Value)
            {
                return (null, null); // Token เป็นของคนละคนกัน (Mismatch) -> จบ
            }

            var user = await _context.Users.FindAsync(userLogin.UserId);
            if (user == null) return (null, null);

            // สร้าง Token ชุดใหม่
            var newAccessToken = _jwtService.CreateAccessToken(user);
            var newRefreshToken = _jwtService.CreateRefreshToken();

            // อัปเดต Refresh Token ในฐานข้อมูล
            userLogin.RefreshToken = newRefreshToken;
            userLogin.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(90); // --- FIX: ต่ออายุวันหมดอายุไปอีก 90 วัน ---
            await _context.SaveChangesAsync();

            return (newAccessToken, newRefreshToken);
        }
        public async Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto)
        {
            var userLogin = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.UserId == userId && ul.ProviderName == "Local");

            if (userLogin == null)
            {
                return (false, "User not found or does not have a local password.");
            }

            if (string.IsNullOrEmpty(userLogin.PasswordHash) ||
                !_passwordHasher.Verify(changePasswordDto.OldPassword, userLogin.PasswordHash))
            {
                return (false, "Incorrect old password.");
            }

            userLogin.PasswordHash = _passwordHasher.Hash(changePasswordDto.NewPassword);
            await _context.SaveChangesAsync();

            return (true, "Password changed successfully.");
        }

        public async Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(ResetPasswordDto dto)
        {
            // ค้นหา User จากเบอร์โทรแทน UserID
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.PhoneNumber == dto.PhoneNumber);
            if (userProfile == null)
            {
                return (false, "User with this phone number not found.");
            }

            var userLogin = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.UserId == userProfile.UserId && ul.ProviderName == "Local");

            if (userLogin == null)
            {
                return (false, "User not found.");
            }

            userLogin.PasswordHash = _passwordHasher.Hash(dto.NewPassword);
            userLogin.RefreshToken = null;
            userLogin.RefreshTokenExpiryTime = null;

            await _context.SaveChangesAsync();
            return (true, "Password has been reset successfully.");
        }

        // --- OTP Section ---

        /// <summary>ให้เทียบเบอร์ไทยได้แม้มี/ไม่มีเลข 0 นำหน้า หรือรูปแบบ +66</summary>
        private static string NormalizePhoneDigits(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var d = new string(raw.Where(char.IsDigit).ToArray());
            if (d.Length == 9)
                return "0" + d;
            if (d.Length == 12 && d.StartsWith("66", StringComparison.Ordinal))
                return "0" + d[2..];
            if (d.Length == 13 && d.StartsWith("660", StringComparison.Ordinal))
                return d[2..];
            return d;
        }

        public async Task<(bool Success, string Message)> ResendOtpAsync(string phoneNumber, int? forUserId = null)
        {
            phoneNumber = phoneNumber?.Trim() ?? string.Empty;
            var wantDigits = NormalizePhoneDigits(phoneNumber);

            UserProfile? userProfile;
            if (forUserId.HasValue)
            {
                userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == forUserId.Value);
                if (userProfile == null) return (false, "User not found.");
                if (!string.Equals(NormalizePhoneDigits(userProfile.PhoneNumber), wantDigits, StringComparison.Ordinal))
                {
                    return (false, "เบอร์โทรไม่ตรงกับบัญชีที่กำลังเชื่อม กรุณาส่ง OTP ใหม่จากหน้าเชื่อมเบอร์");
                }
            }
            else
            {
                // กรณีสมัครด้วยเบอร์ (มักไม่ซ้ำ)
                var allWithDigits = await _context.UserProfiles
                    .AsNoTracking()
                    .Where(p => p.PhoneNumber != null && p.PhoneNumber != "")
                    .Select(p => new { p.UserId, p.PhoneNumber })
                    .ToListAsync();
                var matches = allWithDigits
                    .Where(x => NormalizePhoneDigits(x.PhoneNumber) == wantDigits)
                    .ToList();
                if (matches.Count == 0)
                    return (false, "User not found.");
                userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == matches[0].UserId);
                if (userProfile == null) return (false, "User not found.");
            }

            if (UseOtpBypass())
            {
                var otpEntry = await _context.UserLogins
                    .FirstOrDefaultAsync(ul => ul.UserId == userProfile.UserId && ul.ProviderName == "SMSMKT");
                if (otpEntry == null)
                {
                    otpEntry = new UserLogin
                    {
                        UserId = userProfile.UserId,
                        ProviderName = "SMSMKT",
                        ProviderKey = OtpBypassMarker,
                        PasswordHash = string.Empty
                    };
                    _context.UserLogins.Add(otpEntry);
                }
                else
                {
                    otpEntry.ProviderKey = OtpBypassMarker;
                }

                userProfile.Otpcode = "BYPASS";
                userProfile.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                Console.WriteLine($"SMSMKT: BypassOtp=true — skipped SMS for {phoneNumber}");
                return (true, "OTP bypass mode: ใส่รหัสยืนยัน 6 หลักอะไรก็ได้ (ชั่วคราว).");
            }

            // ตรวจสอบว่า Config ค่ามาครบหรือไม่
            var apiKey = _configuration["SmsMkt:ApiKey"];
            var secretKey = _configuration["SmsMkt:SecretKey"];
            var projectKey = _configuration["SmsMkt:ProjectKey"];

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(projectKey))
            {
                return (false, "SMS Configuration (ApiKey, SecretKey, ProjectKey) is missing.");
            }

            // 2. เรียก SMSMKT API
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("api_key", apiKey);
            client.DefaultRequestHeaders.Add("secret_key", secretKey);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("project_key", projectKey),
                new KeyValuePair<string, string>("phone", phoneNumber)
            });

            var response = await client.PostAsync("https://portal-otp.smsmkt.com/api/otp-send", content);
            var responseString = await response.Content.ReadAsStringAsync();

            // เพิ่ม Log เพื่อดูการตอบกลับจาก SMSMKT
            Console.WriteLine($"SMSMKT Send OTP Response: {responseString}");
            
            // ตัวอย่าง Response: { "code": "200", "result": { "token": "...", "ref_code": "..." } }
            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            
            // --- FIX: เปลี่ยนจาก "200" เป็น "000" ---
            if (root.GetProperty("code").GetString() == "000")
            {
                var token = root.GetProperty("result").GetProperty("token").GetString();
                var refCode = root.GetProperty("result").GetProperty("ref_code").GetString(); // ดึง Ref Code
                
                // 3. เก็บ Token ลง DB (ใช้ UserLogins เป็นที่เก็บชั่วคราว ProviderName="SMSMKT")
                var otpEntry = await _context.UserLogins
                    .FirstOrDefaultAsync(ul => ul.UserId == userProfile.UserId && ul.ProviderName == "SMSMKT");

                if (otpEntry == null)
                {
                    otpEntry = new UserLogin 
                    { 
                        UserId = userProfile.UserId, 
                        ProviderName = "SMSMKT", 
                        ProviderKey = token!, // เก็บ Token ไว้ที่นี่
                        PasswordHash = "" // ไม่ใช้
                    };
                    _context.UserLogins.Add(otpEntry);
                }
                else
                {
                    otpEntry.ProviderKey = token!;
                }

                // บันทึก Ref Code ลงใน UserProfile (เผื่อใช้แสดงผล)
                userProfile.Otpcode = refCode;
                userProfile.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return (true, "OTP sent successfully.");
            }

            // --- FIX: เปลี่ยนจาก "message" เป็น "detail" ---
            return (false, "Failed to send OTP: " + (root.TryGetProperty("detail", out var msg) ? msg.GetString() : "Unknown error"));
        }

        public async Task<(bool Success, string Message)> VerifyOtpAsync(string phoneNumber, string otp)
        {
            phoneNumber = phoneNumber?.Trim() ?? string.Empty;
            var wantDigits = NormalizePhoneDigits(phoneNumber);

            // เฉพาะ user ที่มีแถว SMSMKT ค้าง + เบอร์ตรง (กันกรณีหลาย User ใช้เบอร์เดียวใน DB)
            var pendingPairs = await (
                from ul in _context.UserLogins
                join p in _context.UserProfiles on ul.UserId equals p.UserId
                where ul.ProviderName == "SMSMKT"
                select new { Otp = ul, Profile = p }
            ).ToListAsync();

            var hit = pendingPairs
                .FirstOrDefault(x => NormalizePhoneDigits(x.Profile.PhoneNumber) == wantDigits);

            if (hit == null)
            {
                // แยกข้อความ: ไม่มี user เบอร์นี้ vs มี user แต่ยังไม่ได้ขอ OTP
                var numbers = await _context.UserProfiles
                    .AsNoTracking()
                    .Where(p => p.PhoneNumber != null && p.PhoneNumber != "")
                    .Select(p => p.PhoneNumber!)
                    .ToListAsync();
                var anyProfileDigits = numbers.Any(n => NormalizePhoneDigits(n) == wantDigits);
                if (!anyProfileDigits)
                    return (false, "User not found.");
                return (false, "No OTP request found. Please resend OTP.");
            }

            var otpEntry = hit.Otp;
            var userProfile = hit.Profile;

            if (UseOtpBypass() && string.Equals(otpEntry.ProviderKey, OtpBypassMarker, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(otp) || otp.Trim().Length < 6)
                {
                    return (false, "กรุณากรอกรหัสยืนยัน 6 หลัก (โหมด bypass).");
                }

                userProfile.IsPhoneNumberVerified = true;
                _context.UserLogins.Remove(otpEntry);
                await _context.SaveChangesAsync();
                Console.WriteLine($"SMSMKT: BypassOtp verify accepted for {phoneNumber}");
                return (true, "Phone number verified successfully.");
            }

            // 2. เรียก SMSMKT API เพื่อตรวจสอบ
            var apiKey = _configuration["SmsMkt:ApiKey"];
            var secretKey = _configuration["SmsMkt:SecretKey"];
            
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
            {
                return (false, "SMS Configuration is missing.");
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("api_key", apiKey);
            client.DefaultRequestHeaders.Add("secret_key", secretKey);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", otpEntry.ProviderKey),
                new KeyValuePair<string, string>("otp_code", otp)
                // new KeyValuePair<string, string>("ref_code", userProfile.Otpcode ?? ""), // ref_code ไม่จำเป็นสำหรับการ validate
            });

            var response = await client.PostAsync("https://portal-otp.smsmkt.com/api/otp-validate", content);
            var responseString = await response.Content.ReadAsStringAsync();

            // เพิ่ม Log เพื่อดูการตอบกลับ
            Console.WriteLine($"SMSMKT Verify OTP Response: {responseString}");

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.GetProperty("code").GetString() == "000")
            {
                // 3. ยืนยันสำเร็จ -> อัปเดตสถานะ User
                userProfile.IsPhoneNumberVerified = true;
                
                // ลบ Token ออกเพื่อความสะอาด
                _context.UserLogins.Remove(otpEntry);
                
                await _context.SaveChangesAsync();
                return (true, "Phone number verified successfully.");
            }

            var detail = root.TryGetProperty("detail", out var detailElement) ? detailElement.GetString() : "Invalid OTP code.";
            return (false, detail ?? "Invalid OTP code.");
        }

        // --- Social Login Section ---

        public async Task<(SocialLoginResponseDto? Response, string ErrorMessage)> LoginWithGoogleAsync(GoogleLoginDto dto)
        {
            var identity = await _googleVerifier.VerifyAsync(dto.IdToken);
            if (identity == null)
            {
                return (null, "Google sign-in token ไม่ถูกต้องหรือหมดอายุ");
            }
            return await IssueOrCreateSocialUserAsync(identity);
        }

        public async Task<(SocialLoginResponseDto? Response, string ErrorMessage)> LoginWithAppleAsync(AppleLoginDto dto)
        {
            var identity = await _appleVerifier.VerifyAsync(dto.IdentityToken);
            if (identity == null)
            {
                return (null, "Apple identity token ไม่ถูกต้องหรือหมดอายุ");
            }

            // Apple ส่ง email เฉพาะตอนสมัครครั้งแรก (ผ่าน body ของ POST /auth/apple) — รับมาผูกถ้า identity token ไม่มี
            // และ Apple ส่งชื่อมาเฉพาะครั้งแรกเช่นกัน → frontend ต้องส่ง dto.FullName มาด้วย
            var enriched = identity with
            {
                Email = !string.IsNullOrWhiteSpace(identity.Email) ? identity.Email : dto.Email,
                Name = !string.IsNullOrWhiteSpace(identity.Name) ? identity.Name : dto.FullName
            };
            return await IssueOrCreateSocialUserAsync(enriched);
        }

        /// <summary>
        /// Match-or-create user สำหรับ social provider:
        /// 1. ถ้ามี UserLogin (ProviderName + ProviderKey) ตรงอยู่แล้ว → ออก token ให้เลย
        /// 2. ถ้าไม่เจอ → สร้าง User + UserProfile (เบอร์ยังไม่ verify) + UserLogin ใหม่
        ///    → return RequiresPhoneVerification = true เพื่อให้ frontend ไปหน้าผูกเบอร์โทร
        /// 
        /// หมายเหตุ: เลือกไม่ auto-link ด้วย email เพราะ email ของ user เก่าใน DB ยังไม่ได้ verify
        /// (ระบบเดิมไม่มี email verification flow). User ที่อยากผูก social กับบัญชีเก่า
        /// จะทำผ่านหน้า "Linked Accounts" ใน Settings ภายหลัง (Phase 2)
        /// </summary>
        private async Task<(SocialLoginResponseDto? Response, string ErrorMessage)> IssueOrCreateSocialUserAsync(VerifiedSocialIdentity identity)
        {
            var existingLogin = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.ProviderName == identity.ProviderName && ul.ProviderKey == identity.ProviderKey);

            UserProfile? profile;

            if (existingLogin != null)
            {
                var user = await _context.Users.FindAsync(existingLogin.UserId);
                if (user == null || !user.IsActive)
                {
                    return (null, "บัญชีถูกระงับการใช้งาน");
                }

                var accessToken = _jwtService.CreateAccessToken(user);
                var refreshToken = _jwtService.CreateRefreshToken();

                existingLogin.RefreshToken = refreshToken;
                existingLogin.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(90);
                existingLogin.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.UserId);
                var requiresPhone = profile == null || !profile.IsPhoneNumberVerified || string.IsNullOrEmpty(profile.PhoneNumber);

                return (new SocialLoginResponseDto(
                    AccessToken: accessToken,
                    RefreshToken: refreshToken,
                    RequiresPhoneVerification: requiresPhone,
                    PhoneNumber: profile?.PhoneNumber
                ), string.Empty);
            }

            // First-time signup ผ่าน social
            var newUser = new User { IsActive = true };
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // ตัดชื่อ-สกุล แบบเบื้องต้น (Apple ส่งมาเป็น "Saksit Mukdasanit" / Google ส่ง name เต็ม)
            var (firstName, lastName) = SplitName(identity.Name);

            profile = new UserProfile
            {
                UserId = newUser.UserId,
                PrimaryContactEmail = identity.Email,
                FirstName = firstName,
                LastName = lastName,
                Nickname = !string.IsNullOrWhiteSpace(firstName) ? firstName : null,
                IsPhoneNumberVerified = false,
                // ดึงรูปจาก provider เฉพาะครั้งแรก; รอบถัดไป (existingLogin) ไม่อัปเดต เพื่อไม่ทับรูปที่ user เปลี่ยนเอง
                ProfilePhotoUrl = string.IsNullOrWhiteSpace(identity.ProfilePhotoUrl) ? null : identity.ProfilePhotoUrl.Trim()
            };
            _context.UserProfiles.Add(profile);

            var newRefresh = _jwtService.CreateRefreshToken();
            var newLogin = new UserLogin
            {
                ProviderName = identity.ProviderName,
                ProviderKey = identity.ProviderKey,
                UserId = newUser.UserId,
                PasswordHash = string.Empty, // social login ไม่มีรหัสผ่าน
                ProviderEmail = identity.Email,
                RefreshToken = newRefresh,
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(90)
            };
            _context.UserLogins.Add(newLogin);
            await _context.SaveChangesAsync();

            var newAccess = _jwtService.CreateAccessToken(newUser);
            return (new SocialLoginResponseDto(
                AccessToken: newAccess,
                RefreshToken: newRefresh,
                RequiresPhoneVerification: true,
                PhoneNumber: null
            ), string.Empty);
        }

        private static (string? First, string? Last) SplitName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return (null, null);
            var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => (null, null),
                1 => (parts[0], null),
                _ => (parts[0], parts[1])
            };
        }

        // --- Account Deletion (Apple Guideline 5.1.1(v)) ---

        public const int DeletionGracePeriodDays = 30;

        /// <summary>
        /// ขอลบบัญชี (Soft delete). บัญชีจะถูก lock 30 วัน — user สามารถ login เข้าระบบเพื่อกู้คืนได้
        /// หลังครบ 30 วัน background job จะลบจริง (hard-delete)
        ///
        /// ปฏิเสธหาก:
        /// 1. ผู้ใช้เป็นผู้จัดที่ยังมี active session (ก๊วนที่ยังไม่จบ)
        /// 2. ผู้ใช้มี upcoming session ที่จองไว้ (ก๊วนที่ยังไม่เริ่ม)
        /// 3. ยังมีเงินใน Wallet
        /// </summary>
        public async Task<(bool Success, string Message, DateTime? scheduledDeletionAt)> RequestAccountDeletionAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.OrganizerProfile)
                .Include(u => u.UserWallet)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return (false, "ไม่พบบัญชีผู้ใช้", null);
            if (user.DeletedAt.HasValue)
            {
                var remaining = (user.DeletedAt.Value.AddDays(DeletionGracePeriodDays) - DateTime.UtcNow).Days;
                return (false, $"บัญชีนี้อยู่ในระยะรอลบอยู่แล้ว (เหลืออีก {Math.Max(remaining, 0)} วัน)", null);
            }

            // 1. ตรวจ Wallet balance
            if (user.UserWallet != null && user.UserWallet.Balance > 0)
            {
                return (false, $"กรุณาถอนเงินใน Wallet ออกก่อน (ยอดคงเหลือ {user.UserWallet.Balance:N2} บาท)", null);
            }

            // 2. ตรวจ active session ของผู้จัด (ก๊วนที่กำลังจัดวันนี้หรือกำลังจะมาถึง)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (user.OrganizerProfile != null)
            {
                var activeSessionCount = await _context.GameSessions
                    .Where(s => s.CreatedByUserId == userId && s.SessionDate >= today)
                    .CountAsync();
                if (activeSessionCount > 0)
                {
                    return (false, $"กรุณาจัดการก๊วนที่กำลังจะมาถึง ({activeSessionCount} รายการ) ให้เสร็จก่อนลบบัญชี", null);
                }
            }

            // 3. ตรวจ upcoming session ที่จองไว้ในฐานะผู้เล่น
            var upcomingJoinedCount = await _context.SessionParticipants
                .Include(sp => sp.Session)
                .Where(sp => sp.UserId == userId
                    && sp.Session != null
                    && sp.Session.SessionDate >= today)
                .CountAsync();
            if (upcomingJoinedCount > 0)
            {
                return (false, $"กรุณายกเลิกการจองก๊วนที่กำลังจะมาถึง ({upcomingJoinedCount} รายการ) ก่อนลบบัญชี", null);
            }

            // ผ่านทุกเงื่อนไข → soft-delete
            user.DeletedAt = DateTime.UtcNow;
            user.IsActive = false;
            user.UpdatedDate = DateTime.UtcNow;

            // Invalidate refresh tokens ทุก provider เพื่อบังคับ logout ทุกอุปกรณ์
            var logins = await _context.UserLogins.Where(l => l.UserId == userId).ToListAsync();
            foreach (var login in logins)
            {
                login.RefreshToken = null;
                login.RefreshTokenExpiryTime = null;
            }

            await _context.SaveChangesAsync();
            var scheduled = user.DeletedAt.Value.AddDays(DeletionGracePeriodDays);
            return (true, $"บัญชีจะถูกลบถาวรในวันที่ {scheduled:dd MMM yyyy} (อีก {DeletionGracePeriodDays} วัน). คุณสามารถกู้คืนได้โดย login ก่อนวันดังกล่าว", scheduled);
        }

        /// <summary>
        /// ยกเลิกการขอลบบัญชี (กู้คืนภายใน 30 วัน)
        /// </summary>
        public async Task<(bool Success, string Message)> CancelAccountDeletionAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return (false, "ไม่พบบัญชีผู้ใช้");
            if (!user.DeletedAt.HasValue) return (false, "บัญชีนี้ไม่ได้อยู่ในระยะรอลบ");

            var deadline = user.DeletedAt.Value.AddDays(DeletionGracePeriodDays);
            if (DateTime.UtcNow > deadline)
            {
                return (false, "พ้นระยะกู้คืนแล้ว (เกิน 30 วัน)");
            }

            user.DeletedAt = null;
            user.IsActive = true;
            user.UpdatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return (true, "กู้คืนบัญชีเรียบร้อย");
        }

        /// <summary>
        /// เชื่อมเบอร์โทรเข้ากับบัญชีที่ login ผ่าน social (เบอร์โทรยังไม่ verify; เรียก ResendOtp ต่อเพื่อส่ง OTP)
        /// </summary>
        public async Task<(bool Success, string Message)> LinkPhoneNumberAsync(int userId, string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return (false, "กรุณากรอกเบอร์โทรศัพท์");
            }

            // ตรวจว่าเบอร์นี้ยังไม่ถูกใช้โดย user คนอื่น (ที่ verify แล้ว)
            var conflict = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.PhoneNumber == phoneNumber && p.UserId != userId && p.IsPhoneNumberVerified);
            if (conflict != null)
            {
                return (false, "เบอร์โทรศัพท์นี้ถูกใช้งานแล้ว");
            }

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                return (false, "ไม่พบบัญชีผู้ใช้");
            }

            profile.PhoneNumber = phoneNumber;
            profile.IsPhoneNumberVerified = false;
            profile.UpdatedDate = DateTime.UtcNow;
            profile.UpdatedBy = userId;
            await _context.SaveChangesAsync();

            // ส่ง OTP ทันที — ระบุ userId เพื่อไม่ให้ไปชนแถว User อื่นที่มีเบอร์เดียวกันใน DB
            return await ResendOtpAsync(phoneNumber, userId);
        }
    }
}