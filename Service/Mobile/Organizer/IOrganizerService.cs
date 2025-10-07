using DropInBadAPI.Dtos;
using DropInBadAPI.Models;

namespace DropInBadAPI.Interfaces
{
    public interface IOrganizerService
    {
        Task<OrganizerProfile?> GetOrganizerProfileAsync(int userId);
        Task<bool> IsUserAlreadyOrganizerAsync(int userId);
        Task<OrganizerProfile> RegisterAsync(int userId, OrganizerProfileDto dto);
        Task<OrganizerProfile?> UpdateAsync(int userId, OrganizerProfileDto dto);
    }
}