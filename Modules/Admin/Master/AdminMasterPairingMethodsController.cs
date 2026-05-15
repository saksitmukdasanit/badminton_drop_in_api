using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/master/pairing-methods")]
[Authorize(Roles = "Admin")]
public class AdminMasterPairingMethodsController : ControllerBase
{
    private readonly IGenericService<PairingMethod> _svc;

    public AdminMasterPairingMethodsController(IGenericService<PairingMethod> svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<PairingMethod>>>> List()
    {
        var list = (await _svc.GetAllAsync()).ToList();
        return Ok(new Response<List<PairingMethod>> { Status = 200, Message = "OK", Data = list });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Response<PairingMethod>>> Get(int id)
    {
        var e = await _svc.GetByIdAsync(id);
        if (e == null)
        {
            return NotFound(new Response<PairingMethod> { Status = 404, Message = "ไม่พบ" });
        }

        return Ok(new Response<PairingMethod> { Status = 200, Message = "OK", Data = e });
    }

    [HttpPost]
    public async Task<ActionResult<Response<PairingMethod>>> Create([FromBody] PairingMethodUpsertDto dto)
    {
        var now = DateTime.UtcNow;
        var e = new PairingMethod
        {
            MethodName = dto.MethodName.Trim(),
            IsActive = dto.IsActive,
            CreatedDate = now
        };
        var added = await _svc.AddAsync(e);
        return Ok(new Response<PairingMethod> { Status = 201, Message = "สร้างแล้ว", Data = added });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Response<PairingMethod>>> Update(int id, [FromBody] PairingMethodUpsertDto dto)
    {
        var existing = await _svc.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new Response<PairingMethod> { Status = 404, Message = "ไม่พบ" });
        }

        existing.MethodName = dto.MethodName.Trim();
        existing.IsActive = dto.IsActive;
        existing.UpdatedDate = DateTime.UtcNow;
        var updated = await _svc.UpdateAsync(id, existing);
        return Ok(new Response<PairingMethod> { Status = 200, Message = "บันทึกแล้ว", Data = updated });
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
