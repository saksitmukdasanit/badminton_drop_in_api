using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Repositories
{
    public class OrganizerService : IOrganizerService
    {
        private readonly BadmintonDbContext _context;

        public OrganizerService(BadmintonDbContext context)
        {
            _context = context;
        }

        public async Task<OrganizerProfile?> GetOrganizerProfileAsync(int userId)
        {
            return await _context.OrganizerProfiles.FindAsync(userId);
        }

        public async Task<bool> IsUserAlreadyOrganizerAsync(int userId)
        {
            return await _context.OrganizerProfiles.AnyAsync(op => op.UserId == userId);
        }

        public async Task<OrganizerProfile> RegisterAsync(int userId, OrganizerProfileDto dto)
        {
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
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId
            };

            await _context.OrganizerProfiles.AddAsync(newProfile);
            await _context.SaveChangesAsync();
            return newProfile;
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
    }
}