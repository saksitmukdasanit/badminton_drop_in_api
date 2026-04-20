using DropInBadAPI.Dtos;
using DropInBadAPI.Models;
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

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("profile")]
        public async Task<ActionResult<Response<OrganizerProfileDto>>> GetProfile()
        {
            var profile = await _organizerService.GetOrganizerProfileAsync(GetCurrentUserId());
            if (profile == null) return NotFound(new Response<object> { Status = 404, Message = "Organizer profile not found." });
            return Ok(new Response<OrganizerProfileDto> { Status = 200, Message = "Success", Data = profile });
        }

        [HttpPut("profileUserAndOrganizer")]
        public async Task<ActionResult<Response<OrganizerProfileDto>>> UpdateContactInfo([FromBody] UpdateOrganizerContactDto dto)
        {
            var profile = await _organizerService.UpdateContactInfoAsync(GetCurrentUserId(), dto);
            if (profile == null) return NotFound(new Response<object> { Status = 404, Message = "Organizer profile not found." });
            return Ok(new Response<OrganizerProfileDto> { Status = 200, Message = "Contact info updated.", Data = profile });
        }

        [HttpPut("updateTransferBooking")]
        public async Task<ActionResult<Response<OrganizerProfileDto>>> UpdateTransferInfo([FromBody] UpdateOrganizerTransferDto dto)
        {
            var profile = await _organizerService.UpdateTransferInfoAsync(GetCurrentUserId(), dto);
            if (profile == null) return NotFound(new Response<object> { Status = 404, Message = "Organizer profile not found." });
            return Ok(new Response<OrganizerProfileDto> { Status = 200, Message = "Transfer info updated.", Data = profile });
        }

        [HttpPost("register")]
        public async Task<ActionResult<Response<OrganizerProfileDto>>> Register([FromBody] RegisterOrganizerDto dto)
        {
            var (profile, errorMessage) = await _organizerService.RegisterOrganizerAsync(GetCurrentUserId(), dto);
            if (profile == null) return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            return Ok(new Response<OrganizerProfileDto> { Status = 200, Message = "Registration submitted.", Data = profile });
        }
    }
}