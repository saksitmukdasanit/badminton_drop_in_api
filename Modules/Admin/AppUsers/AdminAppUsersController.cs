using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/app/users")]
[Authorize(Roles = "Admin")]
public class AdminAppUsersController : ControllerBase
{
    private readonly IAdminAppUsersService _svc;

    public AdminAppUsersController(IAdminAppUsersService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<AppUserListItemDto>>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await _svc.ListPagedAsync(search, page, pageSize);
        return Ok(new Response<List<AppUserListItemDto>> { Status = 200, Message = "OK", Data = items, Total = total });
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<Response<AppUserDetailDto>>> Get(int userId)
    {
        var item = await _svc.GetByIdAsync(userId);
        if (item == null)
        {
            return NotFound(new Response<AppUserDetailDto> { Status = 404, Message = "ไม่พบผู้ใช้" });
        }

        return Ok(new Response<AppUserDetailDto> { Status = 200, Message = "OK", Data = item });
    }

    [HttpPost]
    public async Task<ActionResult<Response<AppUserDetailDto>>> Create([FromBody] AppUserCreateDto dto)
    {
        var (data, err) = await _svc.CreateAsync(dto);
        if (data == null)
        {
            return BadRequest(new Response<AppUserDetailDto> { Status = 400, Message = err });
        }

        return Ok(new Response<AppUserDetailDto> { Status = 201, Message = "สร้างแล้ว", Data = data });
    }

    [HttpPut("{userId:int}")]
    public async Task<ActionResult<Response<AppUserDetailDto>>> Update(int userId, [FromBody] AppUserUpdateDto dto)
    {
        var (data, err) = await _svc.UpdateAsync(userId, dto);
        if (data == null)
        {
            return BadRequest(new Response<AppUserDetailDto> { Status = 400, Message = err });
        }

        return Ok(new Response<AppUserDetailDto> { Status = 200, Message = "บันทึกแล้ว", Data = data });
    }

    [HttpDelete("{userId:int}")]
    public async Task<ActionResult<Response<object>>> SoftDelete(int userId)
    {
        var (ok, err) = await _svc.SoftDeleteAsync(userId);
        if (!ok)
        {
            return BadRequest(new Response<object> { Status = 400, Message = err });
        }

        return Ok(new Response<object> { Status = 200, Message = "ระบุว่าบัญชีถูกลบ (นุ่มนวล)" });
    }

    [HttpGet("{userId:int}/wallet-transactions")]
    public async Task<ActionResult<Response<List<AdminWalletTransactionDto>>>> WalletTransactions(
        int userId,
        [FromQuery] short? transactionType,
        [FromQuery] string? refQuery,
        [FromQuery] string? recipientQuery,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await _svc.GetWalletTransactionsAsync(userId, transactionType, refQuery, recipientQuery, fromDate, toDate, page, pageSize);
        return Ok(new Response<List<AdminWalletTransactionDto>>
        {
            Status = 200,
            Message = "OK",
            Data = items,
            Total = total
        });
    }
}
