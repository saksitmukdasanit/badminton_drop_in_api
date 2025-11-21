using DropInBadAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // โดยทั่วไปข้อมูล Master ไม่จำเป็นต้องล็อกอิน
    public class DropdownsController : ControllerBase
    {
        private readonly IDropdownService _dropdownService;

        public DropdownsController(IDropdownService dropdownService)
        {
            _dropdownService = dropdownService;
        }

        [HttpGet("banks")]
        public async Task<IActionResult> GetBanks() => Ok(await _dropdownService.GetBanksAsync());

        [HttpGet("facilities")]
        public async Task<IActionResult> GetFacilities() => Ok(await _dropdownService.GetFacilitiesAsync());

        [HttpGet("gametypes")]
        public async Task<IActionResult> GetGameTypes() => Ok(await _dropdownService.GetGameTypesAsync());

        [HttpGet("pairingmethods")]
        public async Task<IActionResult> GetPairingMethods() => Ok(await _dropdownService.GetPairingMethodsAsync());

        [HttpGet("shuttlecockbrands")]
        public async Task<IActionResult> GetShuttlecockBrands() => Ok(await _dropdownService.GetShuttlecockBrandsAsync());

        [HttpGet("shuttlecockmodels")]
        public async Task<IActionResult> GetShuttlecockModels([FromQuery] int? brandId) => Ok(await _dropdownService.GetShuttlecockModelsAsync(brandId));
    }
}