using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/master/banks")]
[Authorize(Roles = "Admin")]
public class AdminMasterBanksController : ControllerBase
{
    private readonly IGenericService<Bank> _svc;

    public AdminMasterBanksController(IGenericService<Bank> svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<Bank>>>> List()
    {
        var list = (await _svc.GetAllAsync()).ToList();
        return Ok(new Response<List<Bank>> { Status = 200, Message = "OK", Data = list });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Response<Bank>>> Get(int id)
    {
        var e = await _svc.GetByIdAsync(id);
        if (e == null)
        {
            return NotFound(new Response<Bank> { Status = 404, Message = "ไม่พบ" });
        }

        return Ok(new Response<Bank> { Status = 200, Message = "OK", Data = e });
    }

    [HttpPost]
    public async Task<ActionResult<Response<Bank>>> Create([FromBody] BankUpsertDto dto)
    {
        var now = DateTime.UtcNow;
        var e = new Bank
        {
            BankName = dto.BankName.Trim(),
            BankCode = string.IsNullOrWhiteSpace(dto.BankCode) ? null : dto.BankCode.Trim(),
            IsActive = dto.IsActive,
            CreatedDate = now
        };
        var added = await _svc.AddAsync(e);
        return Ok(new Response<Bank> { Status = 201, Message = "สร้างแล้ว", Data = added });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Response<Bank>>> Update(int id, [FromBody] BankUpsertDto dto)
    {
        var existing = await _svc.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new Response<Bank> { Status = 404, Message = "ไม่พบ" });
        }

        existing.BankName = dto.BankName.Trim();
        existing.BankCode = string.IsNullOrWhiteSpace(dto.BankCode) ? null : dto.BankCode.Trim();
        existing.IsActive = dto.IsActive;
        existing.UpdatedDate = DateTime.UtcNow;
        var updated = await _svc.UpdateAsync(id, existing);
        return Ok(new Response<Bank> { Status = 200, Message = "บันทึกแล้ว", Data = updated });
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
