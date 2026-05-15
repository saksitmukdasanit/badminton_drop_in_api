using DropInBadAPI.Data;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/master/shuttlecock-models")]
[Authorize(Roles = "Admin")]
public class AdminMasterShuttlecockModelsController : ControllerBase
{
    private readonly IGenericService<ShuttlecockModel> _svc;
    private readonly BadmintonDbContext _db;

    public AdminMasterShuttlecockModelsController(IGenericService<ShuttlecockModel> svc, BadmintonDbContext db)
    {
        _svc = svc;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<ShuttlecockModel>>>> List()
    {
        var list = (await _svc.GetAllAsync()).ToList();
        return Ok(new Response<List<ShuttlecockModel>> { Status = 200, Message = "OK", Data = list });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Response<ShuttlecockModel>>> Get(int id)
    {
        var e = await _svc.GetByIdAsync(id);
        if (e == null)
        {
            return NotFound(new Response<ShuttlecockModel> { Status = 404, Message = "ไม่พบ" });
        }

        return Ok(new Response<ShuttlecockModel> { Status = 200, Message = "OK", Data = e });
    }

    [HttpPost]
    public async Task<ActionResult<Response<ShuttlecockModel>>> Create([FromBody] ShuttlecockModelUpsertDto dto)
    {
        if (!await _db.ShuttlecockBrands.AnyAsync(b => b.BrandId == dto.BrandId))
        {
            return BadRequest(new Response<ShuttlecockModel> { Status = 400, Message = "ไม่พบแบรนด์" });
        }

        var now = DateTime.UtcNow;
        var e = new ShuttlecockModel
        {
            ModelName = dto.ModelName.Trim(),
            BrandId = dto.BrandId,
            IsActive = dto.IsActive,
            CreatedDate = now
        };
        var added = await _svc.AddAsync(e);
        return Ok(new Response<ShuttlecockModel> { Status = 201, Message = "สร้างแล้ว", Data = added });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Response<ShuttlecockModel>>> Update(int id, [FromBody] ShuttlecockModelUpsertDto dto)
    {
        if (!await _db.ShuttlecockBrands.AnyAsync(b => b.BrandId == dto.BrandId))
        {
            return BadRequest(new Response<ShuttlecockModel> { Status = 400, Message = "ไม่พบแบรนด์" });
        }

        var existing = await _svc.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new Response<ShuttlecockModel> { Status = 404, Message = "ไม่พบ" });
        }

        existing.ModelName = dto.ModelName.Trim();
        existing.BrandId = dto.BrandId;
        existing.IsActive = dto.IsActive;
        existing.UpdatedDate = DateTime.UtcNow;
        var updated = await _svc.UpdateAsync(id, existing);
        return Ok(new Response<ShuttlecockModel> { Status = 200, Message = "บันทึกแล้ว", Data = updated });
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
