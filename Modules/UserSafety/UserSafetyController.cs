using DropInBadAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Modules.UserSafety;

[ApiController]
[Route("api/user-safety")]
[Authorize]
public class UserSafetyController : ControllerBase
{
    private readonly IUserSafetyService _service;

    public UserSafetyController(IUserSafetyService service)
    {
        _service = service;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("report")]
    public async Task<IActionResult> ReportUser([FromBody] ReportUserDto dto)
    {
        var (success, message) = await _service.ReportUserAsync(GetUserId(), dto);
        if (!success) return BadRequest(new Response<object> { Status = 400, Message = message });
        return Ok(new Response<object> { Status = 200, Message = message });
    }

    [HttpPost("block")]
    public async Task<IActionResult> BlockUser([FromBody] BlockUserDto dto)
    {
        var (success, message) = await _service.BlockUserAsync(GetUserId(), dto.BlockedUserId);
        if (!success) return BadRequest(new Response<object> { Status = 400, Message = message });
        return Ok(new Response<object> { Status = 200, Message = message });
    }

    [HttpDelete("block/{blockedUserId:int}")]
    public async Task<IActionResult> UnblockUser(int blockedUserId)
    {
        var (success, message) = await _service.UnblockUserAsync(GetUserId(), blockedUserId);
        if (!success) return BadRequest(new Response<object> { Status = 400, Message = message });
        return Ok(new Response<object> { Status = 200, Message = message });
    }

    [HttpGet("blocks")]
    public async Task<ActionResult<Response<List<BlockedUserItemDto>>>> GetBlockedUsers()
    {
        var data = await _service.GetBlockedUsersAsync(GetUserId());
        return Ok(new Response<List<BlockedUserItemDto>> { Status = 200, Message = "OK", Data = data });
    }
}
