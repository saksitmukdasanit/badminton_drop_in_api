using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/cms/admins")]
[Authorize(Roles = "Admin")]
public class AdminCmsAdminUsersController : AdminControllerBase
{
    private readonly IAdminCmsAdminUsersService _svc;

    public AdminCmsAdminUsersController(IAdminCmsAdminUsersService svc)
    {
        _svc = svc;
    }

    /// <summary>รายการบัญชีแอดมิน CMS (แบ่งหน้า)</summary>
    [HttpGet]
    public async Task<ActionResult<Response<List<CmsAdminUserListItemDto>>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await _svc.ListPagedAsync(search, page, pageSize);
        return Ok(new Response<List<CmsAdminUserListItemDto>>
        {
            Status = 200,
            Message = "OK",
            Data = items,
            Total = total
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Response<CmsAdminUserListItemDto>>> Get(int id)
    {
        var item = await _svc.GetByIdAsync(id);
        if (item == null)
        {
            return NotFound(new Response<CmsAdminUserListItemDto> { Status = 404, Message = "ไม่พบบัญชี" });
        }

        return Ok(new Response<CmsAdminUserListItemDto> { Status = 200, Message = "OK", Data = item });
    }

    [HttpPost]
    public async Task<ActionResult<Response<CmsAdminUserListItemDto>>> Create([FromBody] CmsAdminUserCreateDto dto)
    {
        var (data, err) = await _svc.CreateAsync(dto);
        if (data == null)
        {
            return BadRequest(new Response<CmsAdminUserListItemDto> { Status = 400, Message = err });
        }

        return Ok(new Response<CmsAdminUserListItemDto> { Status = 201, Message = "สร้างแล้ว", Data = data });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Response<CmsAdminUserListItemDto>>> Update(int id, [FromBody] CmsAdminUserUpdateDto dto)
    {
        var adminId = GetCmsAdminId();
        if (adminId == null)
        {
            return Unauthorized(new Response<CmsAdminUserListItemDto> { Status = 401, Message = "ไม่พบผู้ใช้" });
        }

        var (data, err) = await _svc.UpdateAsync(id, dto, adminId.Value);
        if (data == null)
        {
            return BadRequest(new Response<CmsAdminUserListItemDto> { Status = 400, Message = err });
        }

        return Ok(new Response<CmsAdminUserListItemDto> { Status = 200, Message = "บันทึกแล้ว", Data = data });
    }

    /// <summary>ปิดการใช้งานบัญชี (ไม่ลบถาวร)</summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<Response<object>>> Deactivate(int id)
    {
        var adminId = GetCmsAdminId();
        if (adminId == null)
        {
            return Unauthorized(new Response<object> { Status = 401, Message = "ไม่พบผู้ใช้" });
        }

        var (ok, err) = await _svc.DeactivateAsync(id, adminId.Value);
        if (!ok)
        {
            return BadRequest(new Response<object> { Status = 400, Message = err });
        }

        return Ok(new Response<object> { Status = 200, Message = "ปิดการใช้งานแล้ว" });
    }
}

