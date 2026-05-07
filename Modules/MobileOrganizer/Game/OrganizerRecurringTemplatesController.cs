using DropInBadAPI.Dtos;
using DropInBadAPI.Models;
using DropInBadAPI.Service.Mobile.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Mobile;

[ApiController]
[Route("api/organizer/recurring-templates")]
[Authorize]
public class OrganizerRecurringTemplatesController : ControllerBase
{
    private readonly IRecurringGameTemplateService _service;

    public OrganizerRecurringTemplatesController(IRecurringGameTemplateService service)
    {
        _service = service;
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<Response<List<OrganizerRecurringTemplateListDto>>>> List(CancellationToken ct)
    {
        var data = await _service.ListAsync(GetCurrentUserId(), ct);
        return Ok(new Response<List<OrganizerRecurringTemplateListDto>>
        {
            Status = 200,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Response<OrganizerRecurringTemplateDetailDto>>> Get(int id, CancellationToken ct)
    {
        var data = await _service.GetDetailAsync(id, GetCurrentUserId(), ct);
        if (data == null)
            return NotFound(new Response<object> { Status = 404, Message = "ไม่พบ template" });
        return Ok(new Response<OrganizerRecurringTemplateDetailDto> { Status = 200, Message = "OK", Data = data });
    }

    [HttpPost]
    public async Task<ActionResult<Response<OrganizerRecurringTemplateDetailDto>>> Create(
        [FromBody] SaveOrganizerRecurringTemplateDto dto,
        CancellationToken ct)
    {
        try
        {
            var data = await _service.CreateAsync(GetCurrentUserId(), dto, ct);
            return Created(string.Empty, new Response<OrganizerRecurringTemplateDetailDto>
            {
                Status = 201,
                Message = "สร้างก๊วนประจำสำเร็จ",
                Data = data,
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new Response<object> { Status = 400, Message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Response<OrganizerRecurringTemplateDetailDto>>> Update(
        int id,
        [FromBody] SaveOrganizerRecurringTemplateDto dto,
        CancellationToken ct)
    {
        try
        {
            var data = await _service.UpdateAsync(id, GetCurrentUserId(), dto, ct);
            if (data == null)
                return NotFound(new Response<object> { Status = 404, Message = "ไม่พบ template" });
            return Ok(new Response<OrganizerRecurringTemplateDetailDto> { Status = 200, Message = "บันทึกแล้ว", Data = data });
        }
        catch (Exception ex)
        {
            return BadRequest(new Response<object> { Status = 400, Message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<Response<object>>> Delete(int id, CancellationToken ct)
    {
        var ok = await _service.SoftDeleteAsync(id, GetCurrentUserId(), ct);
        if (!ok)
            return NotFound(new Response<object> { Status = 404, Message = "ไม่พบ template" });
        return Ok(new Response<object> { Status = 200, Message = "ปิดการใช้งานแล้ว" });
    }

    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<Response<object>>> SetActive(int id, [FromBody] RecurringTemplateActiveDto body, CancellationToken ct)
    {
        var ok = await _service.SetActiveAsync(id, GetCurrentUserId(), body.IsActive, ct);
        if (!ok)
            return NotFound(new Response<object> { Status = 404, Message = "ไม่พบ template" });
        return Ok(new Response<object> { Status = 200, Message = "อัปเดตสถานะแล้ว" });
    }
}
