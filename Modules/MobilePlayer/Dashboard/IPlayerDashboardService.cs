using DropInBadAPI.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DropInBadAPI.Interfaces
{
    public interface IPlayerDashboardService
    {
       Task<PlayerDashboardDto?> GetPlayerDashboardAsync(int userId);
    }
}