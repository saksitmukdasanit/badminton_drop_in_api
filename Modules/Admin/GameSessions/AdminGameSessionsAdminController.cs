using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/app/game-sessions")]
[Authorize(Roles = "Admin")]
public class AdminGameSessionsAdminController : ControllerBase
{
    private readonly IAdminGameSessionsAdminService _svc;

    public AdminGameSessionsAdminController(IAdminGameSessionsAdminService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<GameSessionAdminListItemDto>>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await _svc.ListPagedAsync(search, page, pageSize);
        return Ok(new Response<List<GameSessionAdminListItemDto>> { Status = 200, Message = "OK", Data = items, Total = total });
    }

    [HttpGet("{sessionId:int}")]
    public async Task<ActionResult<Response<GameSessionAdminDetailDto>>> Get(int sessionId)
    {
        var item = await _svc.GetByIdAsync(sessionId);
        if (item == null)
        {
            return NotFound(new Response<GameSessionAdminDetailDto> { Status = 404, Message = "ไม่พบก๊วน" });
        }

        return Ok(new Response<GameSessionAdminDetailDto> { Status = 200, Message = "OK", Data = item });
    }

    [HttpPut("{sessionId:int}")]
    public async Task<ActionResult<Response<GameSessionAdminDetailDto>>> Update(int sessionId, [FromBody] GameSessionAdminUpdateDto dto)
    {
        var (data, err) = await _svc.UpdateAsync(sessionId, dto);
        if (data == null)
        {
            return BadRequest(new Response<GameSessionAdminDetailDto> { Status = 400, Message = err });
        }

        return Ok(new Response<GameSessionAdminDetailDto> { Status = 200, Message = "บันทึกแล้ว", Data = data });
    }

    /// <summary>ยกเลิกก๊วน (Status = 3)</summary>
    [HttpDelete("{sessionId:int}")]
    public async Task<ActionResult<Response<object>>> Cancel(int sessionId)
    {
        var (ok, err) = await _svc.CancelAsync(sessionId);
        if (!ok)
        {
            return BadRequest(new Response<object> { Status = 400, Message = err });
        }

        return Ok(new Response<object> { Status = 200, Message = "ยกเลิกก๊วนแล้ว" });
    }
}
