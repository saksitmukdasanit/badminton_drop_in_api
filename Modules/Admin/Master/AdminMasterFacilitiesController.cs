using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/master/facilities")]
[Authorize(Roles = "Admin")]
public class AdminMasterFacilitiesController : ControllerBase
{
    private readonly IGenericService<Facility> _svc;

    public AdminMasterFacilitiesController(IGenericService<Facility> svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<Facility>>>> List()
    {
        var list = (await _svc.GetAllAsync()).ToList();
        return Ok(new Response<List<Facility>> { Status = 200, Message = "OK", Data = list });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Response<Facility>>> Get(int id)
    {
        var e = await _svc.GetByIdAsync(id);
        if (e == null)
        {
            return NotFound(new Response<Facility> { Status = 404, Message = "ไม่พบ" });
        }

        return Ok(new Response<Facility> { Status = 200, Message = "OK", Data = e });
    }

    [HttpPost]
    public async Task<ActionResult<Response<Facility>>> Create([FromBody] FacilityUpsertDto dto)
    {
        var now = DateTime.UtcNow;
        var e = new Facility
        {
            FacilityName = dto.FacilityName.Trim(),
            IconUrl = string.IsNullOrWhiteSpace(dto.IconUrl) ? null : dto.IconUrl.Trim(),
            IsActive = dto.IsActive,
            CreatedDate = now
        };
        var added = await _svc.AddAsync(e);
        return Ok(new Response<Facility> { Status = 201, Message = "สร้างแล้ว", Data = added });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Response<Facility>>> Update(int id, [FromBody] FacilityUpsertDto dto)
    {
        var existing = await _svc.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new Response<Facility> { Status = 404, Message = "ไม่พบ" });
        }

        existing.FacilityName = dto.FacilityName.Trim();
        existing.IconUrl = string.IsNullOrWhiteSpace(dto.IconUrl) ? null : dto.IconUrl.Trim();
        existing.IsActive = dto.IsActive;
        existing.UpdatedDate = DateTime.UtcNow;
        var updated = await _svc.UpdateAsync(id, existing);
        return Ok(new Response<Facility> { Status = 200, Message = "บันทึกแล้ว", Data = updated });
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<Response<object>>> Delete(int id)
    {
        var ok = await _svc.DeleteAsync(id);
        if (!ok)
        {
            return NotFound(new Response<object> { Status = 404, Message = "ไม่พบ" });
        }

        return Ok(new Response<object> { Status = 200, Message = "ลบ/ปิดใช้งานแล้ว" });
    }
}
