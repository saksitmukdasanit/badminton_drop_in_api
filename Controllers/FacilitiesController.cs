using DropInBadAPI.Interfaces;
using DropInBadAPI.Models; // << เพิ่ม using สำหรับ Response<T>
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
        public async Task<ActionResult<Response<IEnumerable<Facility>>>> GetFacilities()
        {
            var facilities = await _facilityService.GetAllActiveAsync();
            var response = new Response<IEnumerable<Facility>>
            {
                Status = 200,
                Message = "Facilities retrieved successfully.",
                Data = facilities
            };
            return Ok(response);
        }

        // GET: api/Facilities/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Response<Facility>>> GetFacility(int id)
        {
            var facility = await _facilityService.GetByIdAsync(id);

            if (facility == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Facility not found." });
            }

            return Ok(new Response<Facility> { Status = 200, Message = "Facility retrieved successfully.", Data = facility });
        }

        // POST: api/Facilities
        [HttpPost]
        public async Task<ActionResult<Response<Facility>>> CreateFacility([FromBody] Facility facility)
        {
            if (facility == null)
            {
                return BadRequest(new Response<object> { Status = 400, Message = "Facility data is required." });
            }

            var newFacility = await _facilityService.AddAsync(facility);

            var response = new Response<Facility>
            {
                Status = 201,
                Message = "Facility created successfully.",
                Data = newFacility
            };
            return CreatedAtAction(nameof(GetFacility), new { id = newFacility.FacilityId }, response);
        }

        // PUT: api/Facilities/5
        [HttpPut("{id}")]
        public async Task<ActionResult<Response<Facility>>> UpdateFacility(int id, [FromBody] Facility facility)
        {
            if (id != facility.FacilityId)
            {
                return BadRequest(new Response<object> { Status = 400, Message = "ID in URL does not match ID in body." });
            }

            var updatedFacility = await _facilityService.UpdateAsync(facility);

            if (updatedFacility == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Facility not found to update." });
            }

            return Ok(new Response<Facility> { Status = 200, Message = "Facility updated successfully.", Data = updatedFacility });
        }

        // DELETE: api/Facilities/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<Response<object>>> DeleteFacility(int id)
        {
            var success = await _facilityService.DeleteAsync(id);

            if (!success)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Facility not found to delete." });
            }

            return Ok(new Response<object> { Status = 200, Message = "Facility deleted successfully." });
        }
    }
}