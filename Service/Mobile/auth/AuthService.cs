using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DropInBadAPI.Repositories
{
    public class AuthService : IAuthService
    {
        private readonly BadmintonDbContext _context;
        private readonly IJwtService _jwtService;

        public AuthService(BadmintonDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        public async Task<(bool Success, string ErrorMessage)> InitiateRegistrationAsync(InitiateRegisterDto dto)
        {
            // ตรวจสอบ Username/Phone number ซ้ำ
            if (await _context.UserLogins.AnyAsync(ul => ul.ProviderKey == dto.Username && ul.ProviderName == "Local"))
                return (false, "Username already exists.");
            if (await _context.UserProfiles.AnyAsync(up => up.PhoneNumber == dto.PhoneNumber))
                return (false, "Phone number already exists.");

            var passwordHash = "hashed_" + dto.Password; // Placeholder for actual hashing

            // สร้าง User แต่ยังไม่ Active เต็มตัว
            var newUser = new User { IsActive = false }; // ยังไม่ Active จนกว่าจะยืนยัน OTP
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _context.UserLogins.Add(new UserLogin { ProviderName = "Local", ProviderKey = dto.Username, PasswordHash = passwordHash, UserId = newUser.UserId });

            // สร้าง OTP (ในระบบจริงควรสุ่มตัวเลข 4-6 หลัก)
            var otpCode = "123456"; // Placeholder
            var otpExpiry = DateTime.UtcNow.AddMinutes(5); // OTP มีอายุ 5 นาที

            _context.UserProfiles.Add(new UserProfile { UserId = newUser.UserId, PhoneNumber = dto.PhoneNumber, Otpcode = otpCode, OtpexpiryDate = otpExpiry, IsPhoneNumberVerified = false });

            await _context.SaveChangesAsync();

            // TODO: ณ จุดนี้ ให้เรียก Service ภายนอกเพื่อส่ง SMS OTP ไปที่ dto.PhoneNumber
            // Console.WriteLine($"Sending OTP {otpCode} to {dto.PhoneNumber}");

            return (true, string.Empty);
        }

        public async Task<(string? AccessToken, string? RefreshToken, string ErrorMessage)> VerifyOtpAndLoginAsync(VerifyOtpDto dto)
        {
            var userProfile = await _context.UserProfiles
                .Include(up => up.User) // ดึงข้อมูล User ที่เชื่อมกันมาด้วย
                .FirstOrDefaultAsync(up => up.PhoneNumber == dto.PhoneNumber);

            if (userProfile == null || userProfile.Otpcode != dto.OtpCode || userProfile.OtpexpiryDate < DateTime.UtcNow)
            {
                return (null, null, "Invalid or expired OTP.");
            }

            // ยืนยันสำเร็จ
            userProfile.IsPhoneNumberVerified = true;
            userProfile.User!.IsActive = true; // เปิดใช้งาน User
            userProfile.Otpcode = null; // ล้าง OTP เพื่อความปลอดภัย
            userProfile.OtpexpiryDate = null;

            // สร้าง Token ทั้ง 2 ตัว
            var accessToken = _jwtService.CreateAccessToken(userProfile.User);
            var refreshToken = _jwtService.CreateRefreshToken();

            // บันทึก Refresh Token
            var userLogin = await _context.UserLogins.FirstAsync(ul => ul.UserId == userProfile.UserId && ul.ProviderName == "Local");
            userLogin.RefreshToken = refreshToken;
            userLogin.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();

            return (accessToken, refreshToken, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> CompleteUserProfileAsync(int userId, CompleteProfileDto dto)
        {
            var userProfile = await _context.UserProfiles.FindAsync(userId);
            if (userProfile == null)
            {
                return (false, "User profile not found.");
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
        public async Task<(string? AccessToken, string? RefreshToken)> LoginUserAsync(LoginDto loginDto)
        {
            var userLogin = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.ProviderKey == loginDto.Username && ul.ProviderName == "Local");

            if (userLogin == null) return (null, null);

            var passwordHash = "hashed_" + loginDto.Password; // Placeholder
            if (userLogin.PasswordHash != passwordHash) return (null, null);

            var user = await _context.Users.FindAsync(userLogin.UserId);
            if (user == null) return (null, null);

            // สร้าง Token ทั้ง 2 ตัว
            var accessToken = _jwtService.CreateAccessToken(user);
            var refreshToken = _jwtService.CreateRefreshToken();

            // บันทึก Refresh Token ลงฐานข้อมูล
            userLogin.RefreshToken = refreshToken;
            userLogin.RefreshTokenExpiryTime = DateTime.Now.AddDays(7); // ตั้งวันหมดอายุ
            await _context.SaveChangesAsync();

            return (accessToken, refreshToken);
        }
        public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
        {
            // ดึงข้อมูลจาก UserProfiles และแปลงเป็น DTO เพื่อส่งกลับ
            var profile = await _context.UserProfiles
                .Where(p => p.UserId == userId)
                .Select(p => new UserProfileDto(p.UserId, p.Nickname, p.ProfilePhotoUrl, p.PrimaryContactEmail))
                .FirstOrDefaultAsync();

            return profile;
        }

        public async Task<(string? AccessToken, string? RefreshToken)> RefreshTokenAsync(string accessToken, string refreshToken)
        {
            var principal = _jwtService.GetPrincipalFromExpiredToken(accessToken);
            var userIdString = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (userIdString == null) return (null, null);

            var userLogin = await _context.UserLogins.SingleOrDefaultAsync(u => u.UserId == int.Parse(userIdString));

            if (userLogin == null)
            {
                return (null, null); // Refresh Token ไม่ถูกต้อง หรือหมดอายุ
            }
            if (userLogin == null || userLogin.RefreshToken != refreshToken || userLogin.RefreshTokenExpiryTime <= DateTime.Now)
            {
                return (null, null); // Refresh Token ไม่ถูกต้อง หรือหมดอายุ
            }

            var user = await _context.Users.FindAsync(userLogin.UserId);
            if (user == null) return (null, null);

            // สร้าง Token ชุดใหม่
            var newAccessToken = _jwtService.CreateAccessToken(user);
            var newRefreshToken = _jwtService.CreateRefreshToken();

            // อัปเดต Refresh Token ในฐานข้อมูล
            userLogin.RefreshToken = newRefreshToken;
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

        public async Task<bool> RequestPasswordResetOtpAsync(RequestOtpDto dto)
        {
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.PhoneNumber == dto.PhoneNumber);
            if (userProfile == null)
            {
                // ไม่เจอเบอร์โทรนี้ในระบบ แต่เราจะไม่แจ้ง Error กลับไปเพื่อความปลอดภัย
                // (ป้องกันการสุ่มหาเบอร์โทรในระบบ)
                return true;
            }

            // สร้าง OTP และวันหมดอายุ
            var otpCode = new Random().Next(100000, 999999).ToString(); // สุ่มเลข 6 หลัก
            var otpExpiry = DateTime.UtcNow.AddMinutes(5);

            userProfile.Otpcode = otpCode;
            userProfile.OtpexpiryDate = otpExpiry;
            await _context.SaveChangesAsync();

            // TODO: ณ จุดนี้ ให้เรียก Service ภายนอกเพื่อส่ง SMS OTP ไปที่ dto.PhoneNumber
            Console.WriteLine($"Sending Password Reset OTP {otpCode} to {dto.PhoneNumber}");

            return true;
        }

        public async Task<(string? ResetToken, string ErrorMessage)> VerifyPasswordResetOtpAsync(VerifyOtpDto dto)
        {
            var userProfile = await _context.UserProfiles
                .Include(up => up.User)
                .FirstOrDefaultAsync(up => up.PhoneNumber == dto.PhoneNumber);

            if (userProfile == null || userProfile.Otpcode != dto.OtpCode || userProfile.OtpexpiryDate < DateTime.UtcNow)
            {
                return (null, "Invalid or expired OTP.");
            }

            // OTP ถูกต้อง ให้ล้างค่า OTP ใน DB
            userProfile.Otpcode = null;
            userProfile.OtpexpiryDate = null;
            await _context.SaveChangesAsync();

            // สร้าง Token พิเศษอายุสั้นๆ (เช่น 10 นาที) สำหรับใช้ในการตั้งรหัสผ่านใหม่
            var resetToken = _jwtService.CreateAccessToken(userProfile.User!);

            return (resetToken, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(int userId, ResetPasswordDto dto)
        {
            var userLogin = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.UserId == userId && ul.ProviderName == "Local");

            if (userLogin == null)
            {
                return (false, "User not found.");
            }

            // Hash รหัสผ่านใหม่แล้วบันทึก
            var newPasswordHash = "hashed_" + dto.NewPassword; // Placeholder for actual hashing
            userLogin.PasswordHash = newPasswordHash;

            // เพื่อความปลอดภัย: ล้าง Refresh Token เก่าทิ้ง เพื่อบังคับให้ทุกเครื่อง Login ใหม่
            userLogin.RefreshToken = null;
            userLogin.RefreshTokenExpiryTime = null;

            await _context.SaveChangesAsync();

            return (true, "Password has been reset successfully.");
        }
    }
}