using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _svc;

    public AdminDashboardController(IAdminDashboardService svc)
    {
        _svc = svc;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<Response<AdminDashboardSummaryDto>>> Summary()
    {
        var data = await _svc.GetSummaryAsync();
        return Ok(new Response<AdminDashboardSummaryDto> { Status = 200, Message = "OK", Data = data });
    }
}
