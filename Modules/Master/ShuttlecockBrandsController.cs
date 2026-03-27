using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShuttlecockBrandsController : ControllerBase
    {
        private readonly IGenericService<ShuttlecockBrand> _shuttlecockBrandService;

        public ShuttlecockBrandsController(IGenericService<ShuttlecockBrand> shuttlecockBrandService)
        {
            _shuttlecockBrandService = shuttlecockBrandService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _shuttlecockBrandService.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var shuttlecockBrand = await _shuttlecockBrandService.GetByIdAsync(id);
            if (shuttlecockBrand == null) return NotFound();
            return Ok(shuttlecockBrand);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ShuttlecockBrand shuttlecockBrand)
        {
            var newShuttlecockBrand = await _shuttlecockBrandService.AddAsync(shuttlecockBrand);
            return CreatedAtAction(nameof(GetById), new { id = newShuttlecockBrand.BrandId }, newShuttlecockBrand);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ShuttlecockBrand shuttlecockBrand)
        {
            var updatedShuttlecockBrand = await _shuttlecockBrandService.UpdateAsync(id, shuttlecockBrand);
            if (updatedShuttlecockBrand == null) return NotFound();
            return Ok(updatedShuttlecockBrand);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _shuttlecockBrandService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}