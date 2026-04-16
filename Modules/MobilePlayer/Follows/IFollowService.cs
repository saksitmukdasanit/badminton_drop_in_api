using System.Threading.Tasks;
using System.Collections.Generic;
using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IFollowService
    {
        Task<(bool Success, string ErrorMessage)> ToggleFollowAsync(int followerId, int organizerId);
        Task<IEnumerable<OrganizerSummaryDto>> GetFollowedOrganizersAsync(int followerId);
    }
}