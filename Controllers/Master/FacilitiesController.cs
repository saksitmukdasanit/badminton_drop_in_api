using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacilityController : ControllerBase
    {
        private readonly IGenericService<Facility> _facilitiesService;

        public FacilityController(IGenericService<Facility> facilitiesService)
        {
            _facilitiesService = facilitiesService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _facilitiesService.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var facilities = await _facilitiesService.GetByIdAsync(id);
            if (facilities == null) return NotFound();
            return Ok(facilities);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Facility facilities)
        {
            var newFacility = await _facilitiesService.AddAsync(facilities);
            return CreatedAtAction(nameof(GetById), new { id = newFacility.FacilityId }, newFacility);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Facility facilities)
        {
            var updatedFacility = await _facilitiesService.UpdateAsync(id, facilities);
            if (updatedFacility == null) return NotFound();
            return Ok(updatedFacility);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _facilitiesService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}