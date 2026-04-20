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
    [Route("api/organizer/dashboard")] // ปรับ Route เป็นของฝั่งผู้จัด
    [Authorize]
    public class OrganizerDashboardController : ControllerBase
    {
        private readonly IOrganizerDashboardService _dashboardService;

        public OrganizerDashboardController(IOrganizerDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<Response<OrganizerDashboardDto>>> GetDashboard()
        {
            var dashboardData = await _dashboardService.GetOrganizerDashboardAsync(GetCurrentUserId());
            if (dashboardData == null) return NotFound(new Response<object> { Status = 404, Message = "Organizer profile not found." });
            return Ok(new Response<OrganizerDashboardDto> { Status = 200, Message = "Success", Data = dashboardData });
        }
    }
}