using DropInBadAPI.Dtos;

namespace DropInBadAPI.Service.Mobile.Organizer
{
    public interface IOrganizerService
    {
        Task<OrganizerProfileDto?> GetOrganizerProfileAsync(int userId);
        Task<OrganizerProfileDto?> UpdateContactInfoAsync(int userId, UpdateOrganizerContactDto dto);
        Task<OrganizerProfileDto?> UpdateTransferInfoAsync(int userId, UpdateOrganizerTransferDto dto);
        Task<(OrganizerProfileDto? Data, string ErrorMessage)> RegisterOrganizerAsync(int userId, RegisterOrganizerDto dto);
    }
}