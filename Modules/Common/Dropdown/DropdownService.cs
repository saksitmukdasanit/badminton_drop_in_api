using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Services
{
    public class DropdownService : IDropdownService
    {
        private readonly BadmintonDbContext _context;

        public DropdownService(BadmintonDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DropdownDto>> GetBanksAsync()
        {
            return await _context.Banks
                .Where(x => x.IsActive == true)
                .Select(x => new DropdownDto(x.BankId, x.BankName))
                .ToListAsync();
        }

        public async Task<IEnumerable<DropdownDto>> GetFacilitiesAsync()
        {
            return await _context.Facilities
                .Where(x => x.IsActive == true)
                .Select(x => new DropdownDto(x.FacilityId, x.FacilityName))
                .ToListAsync();
        }

        public async Task<IEnumerable<DropdownDto>> GetGameTypesAsync()
        {
            return await _context.GameTypes
                .Where(x => x.IsActive == true)
                .Select(x => new DropdownDto(x.GameTypeId, x.TypeName))
                .ToListAsync();
        }

        public async Task<IEnumerable<DropdownDto>> GetPairingMethodsAsync()
        {
            return await _context.PairingMethods
                .Where(x => x.IsActive == true)
                .Select(x => new DropdownDto(x.PairingMethodId, x.MethodName))
                .ToListAsync();
        }

        public async Task<IEnumerable<DropdownDto>> GetShuttlecockBrandsAsync()
        {
            return await _context.ShuttlecockBrands
                .Where(x => x.IsActive == true)
                .Select(x => new DropdownDto(x.BrandId, x.BrandName))
                .ToListAsync();
        }

        public async Task<IEnumerable<DropdownDto>> GetShuttlecockModelsAsync(int? brandId)
        {
            var query = _context.ShuttlecockModels.Where(x => x.IsActive == true);

            if (brandId.HasValue)
            {
                query = query.Where(x => x.BrandId == brandId.Value);
            }

            return await query
                .Select(x => new DropdownDto(x.ModelId, x.ModelName))
                .ToListAsync();
        }
    }
}