using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/cms/policy-documents")]
[Authorize(Roles = "Admin")]
public class AdminPoliciesController : ControllerBase
{
    private readonly IAdminPolicyDocumentsService _svc;

    public AdminPoliciesController(IAdminPolicyDocumentsService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<CmsPolicyDocumentDto>>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await _svc.ListPagedAsync(search, page, pageSize);
        return Ok(new Response<List<CmsPolicyDocumentDto>> { Status = 200, Message = "OK", Data = items, Total = total });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Response<CmsPolicyDocumentDto>>> Get(int id)
    {
        var item = await _svc.GetByIdAsync(id);
        if (item == null)
        {
            return NotFound(new Response<CmsPolicyDocumentDto> { Status = 404, Message = "ไม่พบรายการ" });
        }

        return Ok(new Response<CmsPolicyDocumentDto> { Status = 200, Message = "OK", Data = item });
    }

    [HttpPost]
    public async Task<ActionResult<Response<CmsPolicyDocumentDto>>> Create([FromBody] CmsPolicyDocumentCreateDto dto)
    {
        var (data, err) = await _svc.CreateAsync(dto);
        if (data == null)
        {
            return BadRequest(new Response<CmsPolicyDocumentDto> { Status = 400, Message = err });
        }

        return Ok(new Response<CmsPolicyDocumentDto> { Status = 201, Message = "สร้างแล้ว", Data = data });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Response<CmsPolicyDocumentDto>>> Update(int id, [FromBody] CmsPolicyDocumentUpdateDto dto)
    {
        var (data, err) = await _svc.UpdateAsync(id, dto);
        if (data == null)
        {
            return BadRequest(new Response<CmsPolicyDocumentDto> { Status = 400, Message = err });
        }

        return Ok(new Response<CmsPolicyDocumentDto> { Status = 200, Message = "บันทึกแล้ว", Data = data });
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<Response<object>>> Delete(int id)
    {
        var (ok, err) = await _svc.DeleteAsync(id);
        if (!ok)
        {
            return NotFound(new Response<object> { Status = 404, Message = err });
        }

        return Ok(new Response<object> { Status = 200, Message = "ลบแล้ว" });
    }
}

