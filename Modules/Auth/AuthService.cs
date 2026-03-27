using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http;
using System.Text.Json;

namespace DropInBadAPI.Repositories
{
    public class AuthService : IAuthService
    {
        private readonly BadmintonDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AuthService(BadmintonDbContext context, IJwtService jwtService, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _jwtService = jwtService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

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

            var passwordHash = "hashed_" + dto.Password; // Placeholder

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

            var passwordHash = "hashed_" + loginDto.Password; // Placeholder
            if (userLogin.PasswordHash != passwordHash) return (null, null, "Invalid username or password.");

            var user = await _context.Users.FindAsync(userLogin.UserId);
            if (user == null) return (null, null, "User not found.");

            // 2. ตรวจสอบว่ายืนยันเบอร์โทรศัพท์หรือยัง
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.UserId);
            if (userProfile != null && !userProfile.IsPhoneNumberVerified)
            {
                return (null, null, "Phone number not verified. Please verify OTP.");
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

            // 1. ตรวจสอบรหัสผ่านเก่า (ในโค้ดจริงต้องใช้ BCrypt.Verify)
            var oldPasswordHash = "hashed_" + changePasswordDto.OldPassword; // Placeholder
            if (userLogin.PasswordHash != oldPasswordHash)
            {
                return (false, "Incorrect old password.");
            }

            // 2. Hash รหัสผ่านใหม่
            var newPasswordHash = "hashed_" + changePasswordDto.NewPassword; // Placeholder

            // 3. อัปเดตรหัสผ่านใหม่ลงฐานข้อมูล
            userLogin.PasswordHash = newPasswordHash;
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

            var newPasswordHash = "hashed_" + dto.NewPassword; // Placeholder
            userLogin.PasswordHash = newPasswordHash;
            userLogin.RefreshToken = null;
            userLogin.RefreshTokenExpiryTime = null;

            await _context.SaveChangesAsync();
            return (true, "Password has been reset successfully.");
        }

        // --- OTP Section ---

        public async Task<(bool Success, string Message)> ResendOtpAsync(string phoneNumber)
        {
            // 1. ตรวจสอบว่ามี User นี้หรือไม่
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.PhoneNumber == phoneNumber);
            if (userProfile == null) return (false, "User not found.");

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
            // 1. หา User และ Token ที่เก็บไว้
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.PhoneNumber == phoneNumber);
            if (userProfile == null) return (false, "User not found.");

            var otpEntry = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.UserId == userProfile.UserId && ul.ProviderName == "SMSMKT");

            if (otpEntry == null) return (false, "No OTP request found. Please resend OTP.");

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
    }
}