using DropInBadAPI.Dtos;
using DropInBadAPI.Models;

namespace DropInBadAPI.Service.Mobile.Organizer
{
    public interface IOrganizerService
    {
        Task<FullOrganizerProfileDto?> GetOrganizerProfileAsync(int userId);
        Task<(OrganizerProfile? Profile, string? ErrorMessage)> RegisterAsync(int userId, OrganizerProfileDto dto);
        Task<OrganizerProfile?> UpdateAsync(int userId, OrganizerProfileDto dto);
        Task<bool> UpdateProfileAndOrganizerAsync(int userId, ProfileAndOrganizerDto dto);
        Task<OrganizerProfile?> UpdateTransferBookingAsync(int userId, TransferBookingDto dto);
    }
}