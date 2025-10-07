using DropInBadAPI.Models;

namespace DropInBadAPI.Interfaces
{
    public interface IFacilityService
    {
        Task<IEnumerable<Facility>> GetAllActiveAsync();
        Task<Facility?> GetByIdAsync(int id);
        Task<Facility> AddAsync(Facility facility);
        Task<Facility?> UpdateAsync(Facility facility);
        Task<bool> DeleteAsync(int id);
    }
}