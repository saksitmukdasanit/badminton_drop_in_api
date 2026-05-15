using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminAuthService _adminAuth;

    public AdminAuthController(IAdminAuthService adminAuth)
    {
        _adminAuth = adminAuth;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<Response<AdminTokenResponseDto>>> Login([FromBody] AdminLoginDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (data, err) = await _adminAuth.LoginAsync(dto, ip);
        if (data == null)
        {
            return Unauthorized(new Response<AdminTokenResponseDto> { Status = 401, Message = err });
        }

        return Ok(new Response<AdminTokenResponseDto> { Status = 200, Message = "OK", Data = data });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<Response<AdminTokenResponseDto>>> Refresh([FromBody] AdminRefreshDto dto)
    {
        var (data, err) = await _adminAuth.RefreshAsync(dto);
        if (data == null)
        {
            return Unauthorized(new Response<AdminTokenResponseDto> { Status = 401, Message = err });
        }

        return Ok(new Response<AdminTokenResponseDto> { Status = 200, Message = "OK", Data = data });
    }

    [HttpPost("logout")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Response<object>>> Logout()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out var adminId))
        {
            return Unauthorized(new Response<object> { Status = 401, Message = "ไม่พบผู้ใช้" });
        }

        var (ok, err) = await _adminAuth.LogoutAsync(adminId);
        if (!ok)
        {
            return BadRequest(new Response<object> { Status = 400, Message = err });
        }

        return Ok(new Response<object> { Status = 200, Message = "ออกจากระบบแล้ว" });
    }
}
