using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Service.Mobile.Profile
{
    public class ProfileService : IProfileService
    {
        private readonly BadmintonDbContext _context;
        public ProfileService(BadmintonDbContext context) { _context = context; }

        // เราสามารถย้าย GetUserProfileAsync จาก AuthService มาไว้ที่นี่ได้
        public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
        {
            return await _context.UserProfiles
                .Where(p => p.UserId == userId)
                .Include(p => p.User.OrganizerProfile) 
                .Select(p => new UserProfileDto(
                    p.UserId,
                    p.ProfilePhotoUrl,
                    p.PrimaryContactEmail,
                    p.Nickname,
                    p.FirstName,
                    p.LastName,
                    p.Gender.ToString(),
                    // p.Gender == 1 ? "ชาย" :
                    // p.Gender == 2 ? "หญิง" :
                    // p.Gender == 3 ? "อื่นๆ" : null,
                    p.PhoneNumber,
                    p.IsPhoneNumberVerified,
                    p.EmergencyContactName,
                    p.EmergencyContactPhone,
                    p.User.OrganizerProfile == null ? null : p.User.OrganizerProfile != null && p.User.OrganizerProfile.Status == 1
                    ))
                .FirstOrDefaultAsync();
        }

        public async Task<UserProfileDto?> UpdateUserProfileAsync(int userId, UpdateProfileDto dto)
        {
            var profile = await _context.UserProfiles.FindAsync(userId);
            if (profile == null) return null;

            profile.Nickname = dto.Nickname;
            profile.FirstName = dto.FirstName;
            profile.LastName = dto.LastName;
            profile.PrimaryContactEmail = dto.PrimaryContactEmail;
            profile.Gender = (byte)dto.Gender;
            profile.ProfilePhotoUrl = dto.ProfilePhotoUrl;
            profile.EmergencyContactName = dto.EmergencyContactName;
            profile.EmergencyContactPhone = dto.EmergencyContactPhone;
            profile.UpdatedDate = DateTime.UtcNow;
            profile.UpdatedBy = userId;

            await _context.SaveChangesAsync();
            return await GetUserProfileAsync(userId);
        }

        public async Task<(bool Success, string ErrorMessage)> UpdatePhoneNumberAsync(int userId, UpdatePhoneNumberDto dto)
        {
            // 1. ตรวจสอบว่าเบอร์ใหม่นี้มีคนอื่นใช้และยืนยันตัวตนไปแล้วหรือยัง
            if (await _context.UserProfiles.AnyAsync(p => p.PhoneNumber == dto.NewPhoneNumber && p.IsPhoneNumberVerified))
            {
                return (false, "This phone number is already in use.");
            }

            var profile = await _context.UserProfiles.FindAsync(userId);
            if (profile == null)
            {
                return (false, "Profile not found.");
            }

            // 2. อัปเดตเบอร์โทรและสถานะการยืนยัน
            profile.PhoneNumber = dto.NewPhoneNumber;
            profile.IsPhoneNumberVerified = true;
            profile.UpdatedDate = DateTime.UtcNow;
            profile.UpdatedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Phone number updated successfully.");
        }
    }
}