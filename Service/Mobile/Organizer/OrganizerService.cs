using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Service.Mobile.Organizer
{
    public class OrganizerService : IOrganizerService
    {
        private readonly BadmintonDbContext _context;

        public OrganizerService(BadmintonDbContext context)
        {
            _context = context;
        }

        public async Task<FullOrganizerProfileDto?> GetOrganizerProfileAsync(int userId)
        {
            return await _context.OrganizerProfiles.Include(op => op.User)
            .ThenInclude(u => u.UserProfile)
            .Where(op => op.UserId == userId)
            .Select(op => new FullOrganizerProfileDto(
            op.User.UserProfile!.Nickname ?? string.Empty,
            op.User.UserProfile.FirstName ?? string.Empty,
            op.User.UserProfile.LastName ?? string.Empty,
            op.User.UserProfile.PrimaryContactEmail ?? string.Empty,
            op.User.UserProfile.PhoneNumber ?? string.Empty,
            op.User.UserProfile.Gender,
            op.User.UserProfile.EmergencyContactName,
            op.User.UserProfile.EmergencyContactPhone,
            op.ProfilePhotoUrl,
            op.NationalId ?? string.Empty,
            op.BankId,
            op.BankAccountNumber,
            op.BankAccountPhotoUrl,
            op!.PublicPhoneNumber ?? string.Empty,
            op.FacebookLink,
            op.LineId,
            op.PhoneVisibility,
            op.FacebookVisibility,
            op.LineVisibility,
            op.Status
            ))
            .FirstOrDefaultAsync();
        }

        public async Task<bool> IsUserAlreadyOrganizerAsync(int userId)
        {
            return await _context.OrganizerProfiles.AnyAsync(op => op.UserId == userId);
        }

        public async Task<(OrganizerProfile? Profile, string? ErrorMessage)> RegisterAsync(int userId, OrganizerProfileDto dto)
        {
            if (await IsUserAlreadyOrganizerAsync(userId))
            {
                return (null, "This user is already registered as an organizer.");
            }
            var newProfile = new OrganizerProfile
            {
                UserId = userId,
                ProfilePhotoUrl = dto.ProfilePhotoUrl,
                NationalId = dto.NationalId, // ควรมีการเข้ารหัสก่อนบันทึกจริง
                BankId = dto.BankId,
                BankAccountNumber = dto.BankAccountNumber,
                BankAccountPhotoUrl = dto.BankAccountPhotoUrl,
                PublicPhoneNumber = dto.PublicPhoneNumber,
                FacebookLink = dto.FacebookLink,
                LineId = dto.LineId,
                PhoneVisibility = (byte)dto.PhoneVisibility,
                FacebookVisibility = (byte)dto.FacebookVisibility,
                LineVisibility = (byte)dto.LineVisibility,
                Status = 0,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId
            };

            await _context.OrganizerProfiles.AddAsync(newProfile);
            await _context.SaveChangesAsync();
            return (newProfile, null);
        }

        public async Task<OrganizerProfile?> UpdateAsync(int userId, OrganizerProfileDto dto)
        {
            var existingProfile = await _context.OrganizerProfiles.FindAsync(userId);
            if (existingProfile == null)
            {
                return null;
            }

            // Map data from DTO to the existing entity
            existingProfile.ProfilePhotoUrl = dto.ProfilePhotoUrl;
            existingProfile.NationalId = dto.NationalId;
            existingProfile.BankId = dto.BankId;
            existingProfile.BankAccountNumber = dto.BankAccountNumber;
            existingProfile.BankAccountPhotoUrl = dto.BankAccountPhotoUrl;
            existingProfile.PublicPhoneNumber = dto.PublicPhoneNumber;
            existingProfile.FacebookLink = dto.FacebookLink;
            existingProfile.LineId = dto.LineId;
            existingProfile.PhoneVisibility = (byte)dto.PhoneVisibility;
            existingProfile.FacebookVisibility = (byte)dto.FacebookVisibility;
            existingProfile.LineVisibility = (byte)dto.LineVisibility;
            existingProfile.UpdatedDate = DateTime.UtcNow;
            existingProfile.UpdatedBy = userId;

            await _context.SaveChangesAsync();
            return existingProfile;
        }

        public async Task<bool> UpdateProfileAndOrganizerAsync(int userId, ProfileAndOrganizerDto dto)
        {
            var user = await _context.Users
              .Include(u => u.UserProfile)
              .Include(u => u.OrganizerProfile)
              .FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null || user.UserProfile == null || user.OrganizerProfile == null)
            {
                // ไม่เจอ User หรือ User คนนี้ยังไม่ได้เป็นผู้จัด
                return false;
            }

            user.UserProfile.FirstName = dto.FirstName;
            user.UserProfile.LastName = dto.LastName;
            user.UserProfile.PrimaryContactEmail = dto.Email;
            user.UserProfile.Gender = dto.Gender;
            user.UserProfile.EmergencyContactName = dto.EmergencyContactName;
            user.UserProfile.EmergencyContactPhone = dto.EmergencyContactPhone;
            user.UserProfile.UpdatedDate = DateTime.UtcNow;
            user.UserProfile.UpdatedBy = userId;

            user.OrganizerProfile.ProfilePhotoUrl = dto.ProfilePhotoUrl;
            user.OrganizerProfile.PublicPhoneNumber = dto.PublicPhoneNumber;
            user.OrganizerProfile.FacebookLink = dto.FacebookLink;
            user.OrganizerProfile.LineId = dto.LineId;
            user.OrganizerProfile.PhoneVisibility = dto.PhoneVisibility;
            user.OrganizerProfile.FacebookVisibility = dto.FacebookVisibility;
            user.OrganizerProfile.LineVisibility = dto.LineVisibility;
            user.OrganizerProfile.UpdatedDate = DateTime.UtcNow;
            user.OrganizerProfile.UpdatedBy = userId;

            // 5. บันทึกการเปลี่ยนแปลงทั้งหมดลงฐานข้อมูลในครั้งเดียว 💾
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<OrganizerProfile?> UpdateTransferBookingAsync(int userId, TransferBookingDto dto)
        {
            var existingProfile = await _context.OrganizerProfiles.FindAsync(userId);
            if (existingProfile == null)
            {
                return null;
            }

            // Map data from DTO to the existing entity
            existingProfile.NationalId = dto.NationalId;
            existingProfile.BankId = dto.BankId;
            existingProfile.BankAccountNumber = dto.BankAccountNumber;
            existingProfile.BankAccountPhotoUrl = dto.BankAccountPhotoUrl;
            existingProfile.UpdatedDate = DateTime.UtcNow;
            existingProfile.UpdatedBy = userId;

            await _context.SaveChangesAsync();
            return existingProfile;
        }
    }
}