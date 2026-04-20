using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
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

        public async Task<OrganizerProfileDto?> GetOrganizerProfileAsync(int userId)
        {
            var orgProfile = await _context.OrganizerProfiles
                .Include(op => op.Bank)
                .Include(op => op.User)
                    .ThenInclude(u => u.UserProfile)
                .FirstOrDefaultAsync(op => op.UserId == userId);

            if (orgProfile == null) return null;

            return MapToDto(orgProfile);
        }

        public async Task<OrganizerProfileDto?> UpdateContactInfoAsync(int userId, UpdateOrganizerContactDto dto)
        {
            var orgProfile = await _context.OrganizerProfiles
                .Include(op => op.Bank)
                .Include(op => op.User).ThenInclude(u => u.UserProfile)
                .FirstOrDefaultAsync(op => op.UserId == userId);

            if (orgProfile == null) return null;

            // อัปเดตข้อมูลส่วนตัว (User Profile)
            if (orgProfile.User?.UserProfile != null)
            {
                orgProfile.User.UserProfile.Nickname = dto.Nickname;
                orgProfile.User.UserProfile.FirstName = dto.FirstName;
                orgProfile.User.UserProfile.LastName = dto.LastName;
                orgProfile.User.UserProfile.PrimaryContactEmail = dto.PrimaryContactEmail;
                orgProfile.User.UserProfile.Gender = (byte)dto.Gender;
                orgProfile.User.UserProfile.ProfilePhotoUrl = dto.ProfilePhotoUrl;
                orgProfile.User.UserProfile.EmergencyContactName = dto.EmergencyContactName;
                orgProfile.User.UserProfile.EmergencyContactPhone = dto.EmergencyContactPhone;
                orgProfile.User.UserProfile.UpdatedDate = DateTime.UtcNow;
                orgProfile.User.UserProfile.UpdatedBy = userId;
            }

            // อัปเดตข้อมูลติดต่อสาธารณะ (Organizer Profile)
            orgProfile.PublicPhoneNumber = dto.PublicPhoneNumber;
            orgProfile.PhoneVisibility = dto.PhoneVisibility;
            orgProfile.FacebookLink = dto.FacebookLink;
            orgProfile.FacebookVisibility = dto.FacebookVisibility;
            orgProfile.LineId = dto.LineId;
            orgProfile.LineVisibility = dto.LineVisibility;
            orgProfile.UpdatedDate = DateTime.UtcNow;
            orgProfile.UpdatedBy = userId;

            await _context.SaveChangesAsync();
            return MapToDto(orgProfile);
        }

        public async Task<OrganizerProfileDto?> UpdateTransferInfoAsync(int userId, UpdateOrganizerTransferDto dto)
        {
            var orgProfile = await _context.OrganizerProfiles
                .Include(op => op.Bank)
                .Include(op => op.User).ThenInclude(u => u.UserProfile)
                .FirstOrDefaultAsync(op => op.UserId == userId);

            if (orgProfile == null) return null;

            orgProfile.BankId = dto.BankId;
            orgProfile.BankAccountNumber = dto.BankAccountNumber;
            if (!string.IsNullOrEmpty(dto.BankAccountPhotoUrl))
            {
                orgProfile.BankAccountPhotoUrl = dto.BankAccountPhotoUrl;
            }
            orgProfile.UpdatedDate = DateTime.UtcNow;
            orgProfile.UpdatedBy = userId;

            await _context.SaveChangesAsync();

            // โหลดข้อมูลธนาคารใหม่เพื่อส่งชื่อธนาคารกลับไปแสดงผล
            await _context.Entry(orgProfile).Reference(op => op.Bank).LoadAsync();

            return MapToDto(orgProfile);
        }

        public async Task<(OrganizerProfileDto? Data, string ErrorMessage)> RegisterOrganizerAsync(int userId, RegisterOrganizerDto dto)
        {
            var existingOrg = await _context.OrganizerProfiles.FirstOrDefaultAsync(op => op.UserId == userId);
            if (existingOrg != null)
            {
                return (null, "คุณได้สมัครเป็นผู้จัดไปแล้ว");
            }

            var newOrg = new OrganizerProfile
            {
                UserId = userId,
                NationalId = dto.NationalId, 
                BankId = dto.BankId,
                BankAccountNumber = dto.BankAccountNumber,
                BankAccountPhotoUrl = dto.BankAccountPhotoUrl,
                PublicPhoneNumber = dto.PublicPhoneNumber,
                FacebookLink = dto.FacebookLink,
                LineId = dto.LineId,
                Status = 1, // ให้สิทธิ์เป็น 1 (Approved) ใช้งานได้เลยทันที (ปรับเป็น 0 Pending ได้ถ้ามีระบบ Admin อนุมัติ)
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId
            };

            await _context.OrganizerProfiles.AddAsync(newOrg);
            await _context.SaveChangesAsync();

            var result = await GetOrganizerProfileAsync(userId);
            return (result, string.Empty);
        }

        private OrganizerProfileDto MapToDto(OrganizerProfile op)
        {
            return new OrganizerProfileDto(
                op.UserId,
                op.User?.UserProfile?.Nickname ?? "N/A",
                op.User?.UserProfile?.ProfilePhotoUrl,
                op.NationalId,
                op.BankId,
                op.Bank?.BankName,
                op.BankAccountNumber,
                op.BankAccountPhotoUrl,
                op.PublicPhoneNumber,
                (byte)op.PhoneVisibility,
                op.FacebookLink,
                (byte)op.FacebookVisibility,
                op.LineId,
                (byte)op.LineVisibility,
                (byte)op.Status
            );
        }
    }
}