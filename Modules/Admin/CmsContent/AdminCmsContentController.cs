using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/cms/content")]
[Authorize(Roles = "Admin")]
public class AdminCmsContentController : ControllerBase
{
    private readonly IAdminCmsService _cms;

    public AdminCmsContentController(IAdminCmsService cms)
    {
        _cms = cms;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<CmsContentItemDto>>>> List(
        [FromQuery] short? contentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await _cms.ListPagedAsync(contentType, page, pageSize);
        return Ok(new Response<List<CmsContentItemDto>> { Status = 200, Message = "OK", Data = items, Total = total });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Response<CmsContentItemDto>>> Get(int id)
    {
        var item = await _cms.GetByIdAsync(id);
        if (item == null)
        {
            return NotFound(new Response<CmsContentItemDto> { Status = 404, Message = "ไม่พบรายการ" });
        }

        return Ok(new Response<CmsContentItemDto> { Status = 200, Message = "OK", Data = item });
    }

    [HttpPost]
    public async Task<ActionResult<Response<CmsContentItemDto>>> Create([FromBody] CmsContentItemCreateDto dto)
    {
        var adminId = GetAdminId();
        if (adminId == null)
        {
            return Unauthorized(new Response<CmsContentItemDto> { Status = 401, Message = "ไม่พบผู้ใช้" });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (data, err) = await _cms.CreateAsync(adminId.Value, dto, ip);
        if (data == null)
        {
            return BadRequest(new Response<CmsContentItemDto> { Status = 400, Message = err });
        }

        return Ok(new Response<CmsContentItemDto> { Status = 201, Message = "สร้างแล้ว", Data = data });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Response<CmsContentItemDto>>> Update(int id, [FromBody] CmsContentItemUpdateDto dto)
    {
        var adminId = GetAdminId();
        if (adminId == null)
        {
            return Unauthorized(new Response<CmsContentItemDto> { Status = 401, Message = "ไม่พบผู้ใช้" });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (data, err) = await _cms.UpdateAsync(adminId.Value, id, dto, ip);
        if (data == null)
        {
            if (err == "ไม่พบรายการ")
            {
                return NotFound(new Response<CmsContentItemDto> { Status = 404, Message = err });
            }

            return BadRequest(new Response<CmsContentItemDto> { Status = 400, Message = err });
        }

        return Ok(new Response<CmsContentItemDto> { Status = 200, Message = "บันทึกแล้ว", Data = data });
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<Response<object>>> Delete(int id)
    {
        var adminId = GetAdminId();
        if (adminId == null)
        {
            return Unauthorized(new Response<object> { Status = 401, Message = "ไม่พบผู้ใช้" });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (ok, err) = await _cms.DeleteAsync(adminId.Value, id, ip);
        if (!ok)
        {
            return NotFound(new Response<object> { Status = 404, Message = err });
        }

        return Ok(new Response<object> { Status = 200, Message = "ลบแล้ว" });
    }

    private int? GetAdminId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out var id))
        {
            return null;
        }

        return id;
    }
}

