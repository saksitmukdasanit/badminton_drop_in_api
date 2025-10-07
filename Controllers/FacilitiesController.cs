using DropInBadAPI.Interfaces; // << เปลี่ยน
using DropInBadAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacilitiesController : ControllerBase
    {
        private readonly IFacilityService _facilityService;

        public FacilitiesController(IFacilityService facilityService)
        {
            _facilityService = facilityService;
        }

        // GET: api/Facilities
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Facility>>> GetFacilities()
        {
            var facilities = await _facilityService.GetAllActiveAsync();
            return Ok(facilities);
        }

        // GET: api/Facilities/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Facility>> GetFacility(int id)
        {
            var facility = await _facilityService.GetByIdAsync(id);

            if (facility == null)
            {
                return NotFound(); // คืนค่า 404 Not Found ถ้าไม่เจอข้อมูล
            }

            return Ok(facility);
        }

        // POST: api/Facilities
        [HttpPost]
        public async Task<ActionResult<Facility>> CreateFacility([FromBody] Facility facility)
        {
            if (facility == null)
            {
                return BadRequest();
            }

            var newFacility = await _facilityService.AddAsync(facility);

            // คืนค่า 201 Created พร้อมข้อมูลที่เพิ่งสร้าง
            return CreatedAtAction(nameof(GetFacility), new { id = newFacility.FacilityId }, newFacility);
        }

        // PUT: api/Facilities/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFacility(int id, [FromBody] Facility facility)
        {
            if (id != facility.FacilityId)
            {
                return BadRequest("ID in URL does not match ID in body.");
            }

            var updatedFacility = await _facilityService.UpdateAsync(facility);

            if (updatedFacility == null)
            {
                return NotFound();
            }

            return NoContent(); // คืนค่า 204 No Content หมายถึงอัปเดตสำเร็จ
        }

        // DELETE: api/Facilities/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFacility(int id)
        {
            var success = await _facilityService.DeleteAsync(id);

            if (!success)
            {
                return NotFound();
            }

            return NoContent(); // คืนค่า 204 No Content หมายถึงลบสำเร็จ
        }
    }
}