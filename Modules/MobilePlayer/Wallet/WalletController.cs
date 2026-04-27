using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DropInBadAPI.Controllers.MobilePlayer
{
    [ApiController]
    [Route("api/player/wallet")]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;
        public WalletController(IWalletService walletService) { _walletService = walletService; }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("me")]
        public async Task<ActionResult<Response<WalletDto>>> GetMyWallet()
        {
            var wallet = await _walletService.GetMyWalletAsync(GetCurrentUserId());
            return Ok(new Response<WalletDto> { Status = 200, Message = "Success", Data = wallet });
        }

        [HttpPost("withdraw")]
        public async Task<ActionResult<Response<object>>> Withdraw([FromBody] WithdrawRequestDto dto)
        {
            var (success, message) = await _walletService.WithdrawAsync(GetCurrentUserId(), dto.Amount);
            if (!success) return BadRequest(new Response<object> { Status = 400, Message = message });
            return Ok(new Response<object> { Status = 200, Message = message });
        }
    }
}