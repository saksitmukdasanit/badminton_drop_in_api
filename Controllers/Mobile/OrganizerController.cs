using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // *** สำคัญ: ทุก API ในนี้ต้องมีการยืนยันตัวตน (ส่ง JWT Token มาด้วย) ***
    public class OrganizerController : ControllerBase
    {
        private readonly IOrganizerService _organizerService;

        public OrganizerController(IOrganizerService organizerService)
        {
            _organizerService = organizerService;
        }

        private int GetCurrentUserId()
        {
            // ดึง UserID ของคนที่ล็อกอินอยู่ ออกมาจาก Token
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterAsOrganizer([FromBody] OrganizerProfileDto dto)
        {
            var userId = GetCurrentUserId();

            // ตรวจสอบว่าเคยสมัครเป็นผู้จัดแล้วหรือยัง
            if (await _organizerService.IsUserAlreadyOrganizerAsync(userId))
            {
                return BadRequest("This user is already registered as an organizer.");
            }

            var newProfile = await _organizerService.RegisterAsync(userId, dto);
            return CreatedAtAction(nameof(GetOrganizerProfile), new { }, newProfile);
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetOrganizerProfile()
        {
            var userId = GetCurrentUserId();
            var profile = await _organizerService.GetOrganizerProfileAsync(userId);

            if (profile == null)
            {
                return NotFound("Organizer profile not found for this user.");
            }
            return Ok(profile);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateOrganizerProfile([FromBody] OrganizerProfileDto dto)
        {
            var userId = GetCurrentUserId();
            var updatedProfile = await _organizerService.UpdateAsync(userId, dto);

            if (updatedProfile == null)
            {
                return NotFound("Organizer profile not found for this user.");
            }
            return Ok(updatedProfile);
        }
    }
}