using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/app/notifications")]
[Authorize(Roles = "Admin")]
public class AdminNotificationsAdminController : ControllerBase
{
    private readonly IAdminNotificationsAdminService _svc;

    public AdminNotificationsAdminController(IAdminNotificationsAdminService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<NotificationAdminListItemDto>>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await _svc.ListPagedAsync(search, page, pageSize);
        return Ok(new Response<List<NotificationAdminListItemDto>> { Status = 200, Message = "OK", Data = items, Total = total });
    }

    [HttpGet("{notificationId:int}")]
    public async Task<ActionResult<Response<NotificationAdminListItemDto>>> Get(int notificationId)
    {
        var item = await _svc.GetByIdAsync(notificationId);
        if (item == null)
        {
            return NotFound(new Response<NotificationAdminListItemDto> { Status = 404, Message = "ไม่พบแจ้งเตือน" });
        }

        return Ok(new Response<NotificationAdminListItemDto> { Status = 200, Message = "OK", Data = item });
    }

    [HttpPost]
    public async Task<ActionResult<Response<NotificationAdminListItemDto>>> Create([FromBody] NotificationAdminCreateDto dto)
    {
        var (data, err) = await _svc.CreateAndSendAsync(dto);
        if (data == null)
        {
            return BadRequest(new Response<NotificationAdminListItemDto> { Status = 400, Message = err });
        }

        return Ok(new Response<NotificationAdminListItemDto> { Status = 201, Message = "ส่งแล้ว", Data = data });
    }

    [HttpDelete("{notificationId:int}")]
    public async Task<ActionResult<Response<object>>> Delete(int notificationId)
    {
        var (ok, err) = await _svc.DeleteAsync(notificationId);
        if (!ok)
        {
            return NotFound(new Response<object> { Status = 404, Message = err });
        }

        return Ok(new Response<object> { Status = 200, Message = "ลบแล้ว" });
    }
}
