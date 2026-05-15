using DropInBadAPI.Models;
using DropInBadAPI.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/app/organizers")]
[Authorize(Roles = "Admin")]
public class AdminOrganizersController : ControllerBase
{
    private readonly IAdminOrganizersService _svc;

    public AdminOrganizersController(IAdminOrganizersService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<OrganizerListItemDto>>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await _svc.ListPagedAsync(search, page, pageSize);
        return Ok(new Response<List<OrganizerListItemDto>> { Status = 200, Message = "OK", Data = items, Total = total });
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<Response<OrganizerDetailDto>>> Get(int userId)
    {
        var item = await _svc.GetByUserIdAsync(userId);
        if (item == null)
        {
            return NotFound(new Response<OrganizerDetailDto> { Status = 404, Message = "ไม่พบผู้จัด" });
        }

        return Ok(new Response<OrganizerDetailDto> { Status = 200, Message = "OK", Data = item });
    }

    [HttpPost]
    public async Task<ActionResult<Response<OrganizerDetailDto>>> Create([FromBody] OrganizerCreateDto dto)
    {
        var (data, err) = await _svc.CreateAsync(dto);
        if (data == null)
        {
            return BadRequest(new Response<OrganizerDetailDto> { Status = 400, Message = err });
        }

        return Ok(new Response<OrganizerDetailDto> { Status = 201, Message = "สร้างแล้ว", Data = data });
    }

    [HttpPut("{userId:int}")]
    public async Task<ActionResult<Response<OrganizerDetailDto>>> Update(int userId, [FromBody] OrganizerUpdateDto dto)
    {
        var (data, err) = await _svc.UpdateAsync(userId, dto);
        if (data == null)
        {
            return BadRequest(new Response<OrganizerDetailDto> { Status = 400, Message = err });
        }

        return Ok(new Response<OrganizerDetailDto> { Status = 200, Message = "บันทึกแล้ว", Data = data });
    }

    [HttpDelete("{userId:int}")]
    public async Task<ActionResult<Response<object>>> Suspend(int userId)
    {
        var (ok, err) = await _svc.SuspendAsync(userId);
        if (!ok)
        {
            return BadRequest(new Response<object> { Status = 400, Message = err });
        }

        return Ok(new Response<object> { Status = 200, Message = "ระงับสถานะผู้จัดแล้ว (Status=0)" });
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
