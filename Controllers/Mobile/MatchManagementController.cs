using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models; // << เพิ่ม using สำหรับ Response<T>
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
        public async Task<ActionResult<Response<LiveSessionStateDto>>> GetLiveState(int sessionId)
        {
            var liveState = await _matchService.GetLiveStateAsync(sessionId, GetCurrentUserId());
            if (liveState == null)
            {
                return StatusCode(403, new Response<object> { Status = 403, Message = "You do not have permission to view this session's state." });
            }
            return Ok(new Response<LiveSessionStateDto> { Status = 200, Message = "Live state retrieved successfully.", Data = liveState });
        }

        [HttpPost("gamesessions/{sessionId}/matches")]
        public async Task<ActionResult<Response<CurrentlyPlayingMatchDto>>> StartMatch(int sessionId, [FromBody] CreateMatchDto dto)
        {
            try
            {
                var newMatch = await _matchService.StartMatchAsync(sessionId, GetCurrentUserId(), dto);
                return Ok(new Response<CurrentlyPlayingMatchDto> { Status = 200, Message = "Match started successfully.", Data = newMatch });
            }
            catch (Exception ex) 
            {
                return BadRequest(new Response<object> { Status = 400, Message = ex.Message });
            }
        }

        [HttpPut("matches/{matchId}/my-result")]
        public async Task<ActionResult<Response<object>>> SubmitMyResult(int matchId, [FromBody] SubmitResultDto dto)
        {
            var success = await _matchService.SubmitPlayerResultAsync(matchId, GetCurrentUserId(), dto);
            if (!success)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Match or player not found." });
            }
            return Ok(new Response<object> { Status = 200, Message = "Result submitted successfully." });
        }
        
        [HttpPost("participants/{participantType}/{participantId}/checkout")]
        public async Task<ActionResult<Response<BillSummaryDto>>> CheckoutParticipant(string participantType, int participantId)
        {
            if(participantType != "member" && participantType != "guest")
            {
                return BadRequest(new Response<object> { Status = 400, Message = "Participant type must be 'member' or 'guest'." });
            }

            var bill = await _matchService.CheckoutParticipantAsync(participantId, participantType, GetCurrentUserId());
            if (bill == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Participant not found or checkout failed." });
            }
            return Ok(new Response<BillSummaryDto> { Status = 200, Message = "Checkout successful.", Data = bill });
        }
    }
}