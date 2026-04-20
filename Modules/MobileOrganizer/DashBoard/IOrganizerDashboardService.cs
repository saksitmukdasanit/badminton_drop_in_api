using DropInBadAPI.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DropInBadAPI.Interfaces
{
    public interface IOrganizerDashboardService
    {
        Task<OrganizerDashboardDto?> GetOrganizerDashboardAsync(int userId);
    }
}