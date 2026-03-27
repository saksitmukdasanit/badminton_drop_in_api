using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IDropdownService
    {
        Task<IEnumerable<DropdownDto>> GetBanksAsync();
        Task<IEnumerable<DropdownDto>> GetFacilitiesAsync();
        Task<IEnumerable<DropdownDto>> GetGameTypesAsync();
        Task<IEnumerable<DropdownDto>> GetPairingMethodsAsync();
        Task<IEnumerable<DropdownDto>> GetShuttlecockBrandsAsync();
        Task<IEnumerable<DropdownDto>> GetShuttlecockModelsAsync(int? brandId);
    }
}