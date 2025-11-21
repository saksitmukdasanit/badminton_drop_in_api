using DropInBadAPI.Dtos;
using DropInBadAPI.Models; // << เพิ่ม using สำหรับ Response<T>
using DropInBadAPI.Service.Mobile.Organizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Mobile
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
            var (newProfile, errorMessage) = await _organizerService.RegisterAsync(userId, dto);

            if (errorMessage != null)
            {
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }

            // ถ้าสำเร็จ
            var response = new Response<OrganizerProfile>
            {
                Status = 201,
                Message = "Organizer profile created successfully. Awaiting approval.",
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

            return Ok(new Response<FullOrganizerProfileDto> { Status = 200, Message = "Organizer profile retrieved successfully.", Data = profile });
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

        [HttpPut("profileUserAndOrganizer")]
        public async Task<ActionResult<Response<bool>>> UpdateProfileAndOrganizer([FromBody] ProfileAndOrganizerDto dto)
        {
            var userId = GetCurrentUserId();
            var updatedProfile = await _organizerService.UpdateProfileAndOrganizerAsync(userId, dto);

            if (!updatedProfile)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Organizer profile not found for this user." });
            }

            return Ok(new Response<bool> { Status = 200, Message = "Organizer profile updated successfully.", Data = updatedProfile });
        }

         [HttpPut("updateTransferBooking")]
        public async Task<ActionResult<Response<OrganizerProfile?>>> updateTransferBooking([FromBody] TransferBookingDto dto)
        {
            var userId = GetCurrentUserId();
            var updatedProfile = await _organizerService.UpdateTransferBookingAsync(userId, dto);

            if (updatedProfile == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Organizer profile not found for this user." });
            }

            return Ok(new Response<OrganizerProfile?> { Status = 200, Message = "Organizer profile updated successfully.", Data = updatedProfile });
        }
    }
}