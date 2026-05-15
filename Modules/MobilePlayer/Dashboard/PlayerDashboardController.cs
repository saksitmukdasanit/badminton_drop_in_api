using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models; // ใช้สำหรับคลาส Response<T>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DropInBadAPI.Controllers.Mobile
{
    [ApiController]
    [Route("api/player/dashboard")] // ปรับ Route ให้ชัดเจน
    [Authorize]
    public class PlayerDashboardController : ControllerBase
    {
        private readonly IPlayerDashboardService _dashboardService;

        public PlayerDashboardController(IPlayerDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<Response<PlayerDashboardDto>>> GetDashboard()
        {
            var dashboardData = await _dashboardService.GetPlayerDashboardAsync(GetCurrentUserId());
            if (dashboardData == null) return NotFound(new Response<object> { Status = 404, Message = "User profile not found." });
            return Ok(new Response<PlayerDashboardDto> { Status = 200, Message = "Success", Data = dashboardData });
        }

        [HttpGet("organizer-skills")]
        public async Task<ActionResult<Response<List<PlayerOrganizerSkillItemDto>>>> GetOrganizerSkills()
        {
            var items = await _dashboardService.GetPlayerOrganizerSkillsAsync(GetCurrentUserId());
            return Ok(new Response<List<PlayerOrganizerSkillItemDto>> { Status = 200, Message = "Success", Data = items.ToList() });
        }

        [HttpPut("skill-display-organizer")]
        public async Task<ActionResult<Response<object?>>> SetSkillDisplayOrganizer([FromBody] SetPlayerSkillDisplayOrganizerRequestDto? body)
        {
            var (ok, err) = await _dashboardService.SetSkillDisplayOrganizerPreferenceAsync(GetCurrentUserId(), body?.OrganizerUserId);
            if (!ok) return BadRequest(new Response<object?> { Status = 400, Message = err ?? "คำขอไม่ถูกต้อง" });
            return Ok(new Response<object?> { Status = 200, Message = "Success", Data = null });
        }
    }
}