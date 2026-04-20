using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models; // ใช้สำหรับคลาส Response<T>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
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
    }
}