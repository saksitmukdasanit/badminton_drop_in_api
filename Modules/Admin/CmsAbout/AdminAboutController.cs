using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/cms/about")]
[Authorize(Roles = "Admin")]
public class AdminAboutController : ControllerBase
{
    private readonly IAdminAboutSettingsService _svc;

    public AdminAboutController(IAdminAboutSettingsService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<Response<CmsAboutSettingsDto>>> Get()
    {
        var data = await _svc.GetAsync();
        return Ok(new Response<CmsAboutSettingsDto> { Status = 200, Message = "OK", Data = data });
    }

    [HttpPut]
    public async Task<ActionResult<Response<CmsAboutSettingsDto>>> Save([FromBody] CmsAboutSettingsUpdateDto dto)
    {
        var data = await _svc.SaveAsync(dto);
        return Ok(new Response<CmsAboutSettingsDto> { Status = 200, Message = "บันทึกแล้ว", Data = data });
    }
}

