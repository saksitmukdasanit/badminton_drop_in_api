using DropInBadAPI.Dtos;
using DropInBadAPI.Models;
using DropInBadAPI.Service.MobilePlayer.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DropInBadAPI.Hubs;
using DropInBadAPI.Data; // สำหรับ BadmintonDbContext
using DropInBadAPI.Interfaces; // สำหรับ IMatchManagementService
using System.Security.Claims;

namespace DropInBadAPI.Controllers.MobilePlayer
{
    [ApiController]
    [Route("api/player/gamesessions")]
    [Authorize]
    public class PlayerGameSessionsController : ControllerBase
    {
        private readonly IPlayerGameSessionService _playerSessionService;
        private readonly BadmintonDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<ManagementGameHub> _hubContext;
        private readonly IMatchManagementService _matchManagementService;

        public PlayerGameSessionsController(
            IPlayerGameSessionService playerSessionService,
            BadmintonDbContext context,
            IConfiguration configuration,
            IHubContext<ManagementGameHub> hubContext,
            IMatchManagementService matchManagementService)
        {
            _playerSessionService = playerSessionService;
            _context = context;
            _configuration = configuration;
            _hubContext = hubContext;
            _matchManagementService = matchManagementService;
        }
        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("upcoming")]
        [AllowAnonymous]
        public async Task<ActionResult<Response<IEnumerable<UpcomingSessionCardDto>>>> GetUpcomingSessions([FromQuery] string? keyword = null, [FromQuery] string? sortBy = null, [FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) != null
             ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
             : (int?)null;

            var sessions = await _playerSessionService.GetUpcomingSessionsAsync(currentUserId, keyword, sortBy, page, limit);
            return Ok(new Response<IEnumerable<UpcomingSessionCardDto>> { Status = 200, Message = "Upcoming sessions retrieved successfully.", Data = sessions });
        }

        [HttpGet("my")]
        public async Task<ActionResult<Response<MyGameSessionsResponseDto>>> GetMySessions()
        {
            var sessions = await _playerSessionService.GetMySessionsAsync(GetCurrentUserId());
            return Ok(new Response<MyGameSessionsResponseDto> { Status = 200, Message = "My sessions retrieved successfully.", Data = sessions });
        }

        [HttpGet("history")]
        public async Task<ActionResult<Response<IEnumerable<UpcomingSessionCardDto>>>> GetHistorySessions([FromQuery] string? keyword = null, [FromQuery] string? sortBy = null, [FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            var sessions = await _playerSessionService.GetHistorySessionsAsync(GetCurrentUserId(), keyword, sortBy, page, limit);
            return Ok(new Response<IEnumerable<UpcomingSessionCardDto>> { Status = 200, Message = "History sessions retrieved successfully.", Data = sessions });
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Response<PlayerGameSessionViewDto>>> GetSessionForPlayer(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) != null
             ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
             : (int?)null;

            var session = await _playerSessionService.GetSessionForPlayerViewAsync(id, currentUserId);
            if (session == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Session not found." });
            }
            return Ok(new Response<PlayerGameSessionViewDto> { Status = 200, Message = "Session retrieved successfully.", Data = session });
        }

        [HttpGet("{id}/history-detail")]
        public async Task<ActionResult<Response<PlayerHistoryDetailDto>>> GetHistoryDetail(int id)
        {
            var detail = await _playerSessionService.GetHistoryDetailAsync(id, GetCurrentUserId());
            if (detail == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "History detail not found." });
            }
            return Ok(new Response<PlayerHistoryDetailDto> { Status = 200, Message = "History detail retrieved successfully.", Data = detail });
        }

        [HttpPost("{id}/join")]
        public async Task<ActionResult<Response<JoinSessionResponseDto>>> JoinSession(int id, [FromBody] PlayerJoinSessionRequestDto dto)
        {
            var (data, errorMessage) = await _playerSessionService.JoinSessionAsync(id, GetCurrentUserId(), dto);
            if (data == null)
            {
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }
            return Ok(new Response<JoinSessionResponseDto> { Status = 200, Message = data.StatusMessage, Data = data });
        }

        [HttpDelete("{id}/cancel")]
        public async Task<ActionResult<Response<object>>> CancelBooking(int id)
        {
            var (success, errorMessage) = await _playerSessionService.CancelBookingAsync(id, GetCurrentUserId());
            if (!success) return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            return Ok(new Response<object> { Status = 200, Message = "Your booking has been cancelled." });
        }

        [HttpPost("{id}/checkin")]
        public async Task<ActionResult<Response<object>>> PlayerCheckin(int id, [FromBody] PlayerCheckinRequestDto dto)
        {
            var (success, errorMessage) = await _playerSessionService.PlayerCheckinAsync(id, GetCurrentUserId(), dto.ScannedQrCode);
            if (!success) return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            return Ok(new Response<object> { Status = 200, Message = "Check-in successful." });
        }

        [HttpGet("{id}/live-state")]
        public async Task<ActionResult<Response<LiveSessionStateDto>>> GetLiveState(int id, [FromServices] IMatchManagementService matchService, [FromServices] BadmintonDbContext dbContext)
        {
            var session = await dbContext.GameSessions.FindAsync(id);
            if (session == null) return NotFound(new Response<object> { Status = 404, Message = "Session not found." });

            // ดึง Live State จาก Service เดิมของผู้จัดมาใช้ให้ฝั่ง Player อ่านได้อย่างเดียว
            var liveState = await matchService.GetLiveStateAsync(id, session.CreatedByUserId);
            return Ok(new Response<LiveSessionStateDto> { Status = 200, Message = "Live state retrieved.", Data = liveState });
        }

        [HttpGet("{id}/my-bill")]
        public async Task<ActionResult<Response<PlayerBillPreviewDto>>> GetMyBill(int id)
        {
            var bill = await _playerSessionService.GetMyBillPreviewAsync(id, GetCurrentUserId());
            return Ok(new Response<PlayerBillPreviewDto> { Status = 200, Message = "Bill retrieved.", Data = bill });
        }

        [HttpGet("{id}/my-stats")]
        public async Task<ActionResult<Response<PlayerStatsDto>>> GetMyStats(int id)
        {
            var stats = await _playerSessionService.GetMyStatsAsync(id, GetCurrentUserId());
            return Ok(new Response<PlayerStatsDto> { Status = 200, Message = "Stats retrieved.", Data = stats });
        }

        [HttpPost("matches/{matchId}/submit-result")]
        public async Task<ActionResult<Response<object>>> SubmitMatchResult(int matchId, [FromBody] SubmitMatchResultDto dto)
        {
            var (success, errorMessage) = await _playerSessionService.SubmitMatchResultAsync(matchId, GetCurrentUserId(), dto);
            if (!success) return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            return Ok(new Response<object> { Status = 200, Message = "Result saved successfully." });
        }

        [HttpPost("{id}/checkout-and-pay")]
        public async Task<ActionResult<Response<object>>> CheckoutAndPay(int id, [FromBody] PlayerPaymentRequestDto dto)
        {
            var (success, errorMessage) = await _playerSessionService.CheckoutAndPayAsync(id, GetCurrentUserId(), dto);
            
            if (!success)
            {
                if (errorMessage.Contains("not found") || errorMessage.Contains("not part of")) return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
                return StatusCode(500, new Response<object> { Status = 500, Message = errorMessage });
            }
            
            return Ok(new Response<object> { Status = 200, Message = errorMessage });
        }

        [HttpPost("{id}/toggle-pause")]
        public async Task<ActionResult<Response<object>>> TogglePause(int id, [FromBody] TogglePauseRequestDto dto)
        {
            var (success, errorMessage) = await _playerSessionService.TogglePauseAsync(id, GetCurrentUserId(), dto.IsPaused);
            if (!success) return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            return Ok(new Response<object> { Status = 200, Message = "Pause state updated." });
        }
    }
}