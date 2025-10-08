using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models; // << เพิ่ม using สำหรับ Response<T>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrganizerController : ControllerBase
    {
        private readonly IOrganizerService _organizerService;

        public OrganizerController(IOrganizerService organizerService)
        {
            _organizerService = organizerService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        [HttpPost("register")]
        public async Task<ActionResult<Response<OrganizerProfile>>> RegisterAsOrganizer([FromBody] OrganizerProfileDto dto)
        {
            var userId = GetCurrentUserId();

            if (await _organizerService.IsUserAlreadyOrganizerAsync(userId))
            {
                return BadRequest(new Response<object> { Status = 400, Message = "This user is already registered as an organizer." });
            }

            var newProfile = await _organizerService.RegisterAsync(userId, dto);
            var response = new Response<OrganizerProfile>
            {
                Status = 201,
                Message = "Organizer profile created successfully.",
                Data = newProfile
            };
            return CreatedAtAction(nameof(GetOrganizerProfile), new { }, response);
        }

        [HttpGet("profile")]
        public async Task<ActionResult<Response<OrganizerProfile>>> GetOrganizerProfile()
        {
            var userId = GetCurrentUserId();
            var profile = await _organizerService.GetOrganizerProfileAsync(userId);

            if (profile == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Organizer profile not found for this user." });
            }
            
            return Ok(new Response<OrganizerProfile> { Status = 200, Message = "Organizer profile retrieved successfully.", Data = profile });
        }

        [HttpPut("profile")]
        public async Task<ActionResult<Response<OrganizerProfile>>> UpdateOrganizerProfile([FromBody] OrganizerProfileDto dto)
        {
            var userId = GetCurrentUserId();
            var updatedProfile = await _organizerService.UpdateAsync(userId, dto);

            if (updatedProfile == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Organizer profile not found for this user." });
            }

            return Ok(new Response<OrganizerProfile> { Status = 200, Message = "Organizer profile updated successfully.", Data = updatedProfile });
        }
    }
}