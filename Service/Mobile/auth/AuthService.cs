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

        public async Task<(string? AccessToken, string? RefreshToken, string ErrorMessage)> RegisterAsync(InitiateRegisterDto dto)
        {
            if (await _context.UserLogins.AnyAsync(ul => ul.ProviderKey == dto.Username && ul.ProviderName == "Local"))
                return (null, null, "Username already exists.");
            if (await _context.UserProfiles.AnyAsync(up => up.PhoneNumber == dto.PhoneNumber))
                return (null, null, "Phone number already exists.");

            var passwordHash = "hashed_" + dto.Password; // Placeholder

            var newUser = new User { IsActive = true }; // Active ได้เลย
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _context.UserProfiles.Add(new UserProfile { UserId = newUser.UserId, PhoneNumber = dto.PhoneNumber, IsPhoneNumberVerified = true }); // Verified ได้เลย

            var userLogin = new UserLogin { ProviderName = "Local", ProviderKey = dto.Username, PasswordHash = passwordHash, UserId = newUser.UserId };

            // สร้างและบันทึก Refresh Token
            var refreshToken = _jwtService.CreateRefreshToken();
            userLogin.RefreshToken = refreshToken;
            userLogin.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(90);
            _context.UserLogins.Add(userLogin);

            await _context.SaveChangesAsync();

            // สร้าง Access Token แล้วส่งกลับไป
            var accessToken = _jwtService.CreateAccessToken(newUser);

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
            userLogin.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(90); // ตั้งวันหมดอายุ
            await _context.SaveChangesAsync();

            return (accessToken, refreshToken);
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
            var principal = _jwtService.GetPrincipalFromExpiredToken(accessToken);
            var userIdString = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (userIdString == null) return (null, null);

            var userLogin = await _context.UserLogins.SingleOrDefaultAsync(u => u.UserId == int.Parse(userIdString));

            if (userLogin == null)
            {
                return (null, null); // Refresh Token ไม่ถูกต้อง หรือหมดอายุ
            }
            if (userLogin == null || userLogin.RefreshToken != refreshToken || userLogin.RefreshTokenExpiryTime <= DateTime.UtcNow)
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
    }
}