using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/")]
    [Authorize]
    public class MatchManagementController : ControllerBase
    {
        private readonly IMatchManagementService _matchService;
        public MatchManagementController(IMatchManagementService matchService) { _matchService = matchService; }
        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("gamesessions/{sessionId}/live-state")]
        public async Task<IActionResult> GetLiveState(int sessionId)
        {
            var liveState = await _matchService.GetLiveStateAsync(sessionId, GetCurrentUserId());
            if (liveState == null) return Forbid();
            return Ok(liveState);
        }

        [HttpPost("gamesessions/{sessionId}/matches")]
        public async Task<IActionResult> StartMatch(int sessionId, [FromBody] CreateMatchDto dto)
        {
            try
            {
                var newMatch = await _matchService.StartMatchAsync(sessionId, GetCurrentUserId(), dto);
                return Ok(newMatch);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPut("matches/{matchId}/my-result")]
        public async Task<IActionResult> SubmitMyResult(int matchId, [FromBody] SubmitResultDto dto)
        {
            var success = await _matchService.SubmitPlayerResultAsync(matchId, GetCurrentUserId(), dto);
            if (!success) return NotFound("Match or player not found.");
            return NoContent();
        }
        
        // ตัวอย่าง: /api/participants/member/123/checkout
        [HttpPost("participants/{participantType}/{participantId}/checkout")]
        public async Task<IActionResult> CheckoutParticipant(string participantType, int participantId)
        {
            if(participantType != "member" && participantType != "guest")
            {
                return BadRequest("Participant type must be 'member' or 'guest'.");
            }

            var bill = await _matchService.CheckoutParticipantAsync(participantId, participantType, GetCurrentUserId());
            if (bill == null) return NotFound("Participant not found or checkout failed.");
            return Ok(bill);
        }
    }
}