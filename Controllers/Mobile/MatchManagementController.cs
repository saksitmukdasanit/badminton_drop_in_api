using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models; // << เพิ่ม using สำหรับ Response<T>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Mobile
{
    [ApiController]
    [Route("api/")]
    [Authorize]
    public class MatchManagementController : ControllerBase
    {
        private readonly IMatchManagementService _matchService;
        private readonly IMatchRecommenderService _recommenderService;
        public MatchManagementController(IMatchManagementService matchService, IMatchRecommenderService recommenderService)
        {
            _matchService = matchService;
            _recommenderService = recommenderService;
        }
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

        [HttpPut("matches/{matchId}/end")]
        public async Task<ActionResult<Response<object>>> EndMatch(int matchId)
        {
            var success = await _matchService.EndMatchAsync(matchId, GetCurrentUserId());
            if (!success)
            {
                return StatusCode(403, new Response<object> { Status = 403, Message = "Match not found or you do not have permission to end it." });
            }
            return Ok(new Response<object> { Status = 200, Message = "Match ended successfully." });
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
        
        // --- NEW: API สำหรับดูยอด (Preview) ---
        [HttpGet("participants/{participantType}/{participantId}/bill-preview")]
        public async Task<ActionResult<Response<BillSummaryDto>>> GetBillPreview(string participantType, int participantId)
        {
            if (!participantType.Equals("member", StringComparison.OrdinalIgnoreCase) && 
                !participantType.Equals("guest", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new Response<object> { Status = 400, Message = "Participant type must be 'member' or 'guest'." });
            }

            var bill = await _matchService.GetParticipantBillPreviewAsync(participantType.ToLower(), participantId, GetCurrentUserId());
            if (bill == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Participant not found." });
            }
            return Ok(new Response<BillSummaryDto> { Status = 200, Message = "Bill preview retrieved successfully.", Data = bill });
        }

        // --- UPDATED: API สำหรับเช็คบิลจริง (Checkout) ---
        [HttpPost("participants/{participantType}/{participantId}/checkout")]
        public async Task<ActionResult<Response<BillSummaryDto>>> CheckoutParticipant(string participantType, int participantId, [FromBody] CheckoutRequestDto? dto = null)
        {
            // FIX: ปรับให้รองรับตัวพิมพ์เล็ก/ใหญ่ (Case-Insensitive)
            if (!participantType.Equals("member", StringComparison.OrdinalIgnoreCase) && 
                !participantType.Equals("guest", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new Response<object> { Status = 400, Message = "Participant type must be 'member' or 'guest'." });
            }

            try
            {
                var bill = await _matchService.CheckoutParticipantAsync(participantType.ToLower(), participantId, GetCurrentUserId(), dto);
                if (bill == null)
                {
                    return NotFound(new Response<object> { Status = 404, Message = "Participant not found or checkout failed." });
                }
                return Ok(new Response<BillSummaryDto> { Status = 200, Message = "Checkout successful.", Data = bill });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new Response<object> { Status = 500, Message = $"Internal Server Error: {ex.Message}" });
            }
        }

        // --- NEW: API สำหรับบันทึกการชำระเงิน (Pay) ---
        [HttpPost("bills/{billId}/pay")]
        public async Task<ActionResult<Response<object>>> PayBill(int billId, [FromBody] PaymentRequestDto dto)
        {
            var success = await _matchService.PayBillAsync(billId, GetCurrentUserId(), dto);
            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = "Payment failed or bill not found." });
            }
            return Ok(new Response<object> { Status = 200, Message = "Payment recorded successfully." });
        }

        // --- NEW: API สำหรับยกเลิกบิล ---
        [HttpPut("bills/{billId}/cancel")]
        public async Task<ActionResult<Response<object>>> CancelBill(int billId)
        {
            // หมายเหตุ: ต้องเพิ่ม CancelBillAsync ใน Interface IMatchManagementService ด้วยนะครับ
            var success = await _matchService.CancelBillAsync(billId, GetCurrentUserId());
            if (!success) return BadRequest(new Response<object> { Status = 400, Message = "Failed to cancel bill." });
            return Ok(new Response<object> { Status = 200, Message = "Bill cancelled." });
        }

        [HttpPost("gamesessions/{sessionId}/checkin")]
        public async Task<ActionResult<Response<object>>> CheckinParticipant(int sessionId, [FromBody] CheckinDto dto)
        {
            var (success, message) = await _matchService.CheckinParticipantAsync(sessionId, dto);
            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = message });
            }
            return Ok(new Response<object> { Status = 200, Message = message });
        }

        [HttpPost("gamesessions/{sessionId}/walkin-guests")]
        public async Task<ActionResult<Response<WaitingPlayerDto>>> AddWalkinGuest(int sessionId, [FromBody] AddWalkinDto dto)
        {
            var newGuest = await _matchService.AddWalkinGuestAsync(sessionId, GetCurrentUserId(), dto);
            return Ok(new Response<WaitingPlayerDto> { Status = 200, Message = "Walk-in guest added successfully.", Data = newGuest });
        }

        [HttpGet("organizer/previous-guests")]
        public async Task<ActionResult<Response<List<GuestSuggestionDto>>>> GetPreviousGuests([FromQuery] string? query)
        {
            // Fallback: ถ้า query เป็น null ลองดึงจาก Request โดยตรง (เผื่อมีปัญหา Binding)
            if (string.IsNullOrEmpty(query) && Request.Query.ContainsKey("query"))
            {
                query = Request.Query["query"].ToString();
            }

            var searchTerm = query ?? "";
            var guests = await _matchService.SearchPreviousGuestsAsync(GetCurrentUserId(), searchTerm);
            
            // DEBUG: ส่งค่า query กลับมาดูว่า Server เห็นเป็นอะไร
            return Ok(new Response<List<GuestSuggestionDto>> { Status = 200, Message = $"Guests retrieved. (Server received query: '{searchTerm}')", Data = guests });
        }

        [HttpPut("participants/{participantType}/{participantId}/skill")]
        public async Task<ActionResult<Response<object>>> UpdateParticipantSkill(string participantType, int participantId, [FromBody] UpdateParticipantSkillDto dto)
        {
            if (participantType != "member" && participantType != "guest")
            {
                return BadRequest(new Response<object> { Status = 400, Message = "Participant type must be 'member' or 'guest'." });
            }
            var success = await _matchService.UpdateParticipantSkillAsync(participantType, participantId, dto);
            if (!success)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Participant not found." });
            }
            return Ok(new Response<object> { Status = 200, Message = "Skill level updated successfully." });
        }

        [HttpGet("gamesessions/{sessionId}/suggest-matches")]
        public async Task<ActionResult<Response<List<RecommendedMatchDto>>>> SuggestMatches(int sessionId, [FromQuery] SuggestionCriteria criteria)
        {
            var recommendations = await _recommenderService.SuggestMatchesAsync(sessionId, criteria);
            if (recommendations == null || !recommendations.Any())
            {
                return Ok(new Response<List<RecommendedMatchDto>> { Status = 200, Message = "ไม่พบคำแนะนำการจัดคู่ที่เหมาะสม หรือผู้เล่นไม่เพียงพอ", Data = new List<RecommendedMatchDto>() });
            }
            return Ok(new Response<List<RecommendedMatchDto>> { Status = 200, Message = "พบคำแนะนำการจัดคู่", Data = recommendations });
        }

        [HttpGet("gamesessions/{sessionId}/player-stats/{participantType}/{participantId}")]
        public async Task<ActionResult<Response<PlayerSessionStatsDto>>> GetPlayerStats(int sessionId, string participantType, int participantId)
        {
            var stats = await _matchService.GetPlayerSessionStatsAsync(sessionId, participantType, participantId);
            if (stats == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Player not found in this session." });
            }
            return Ok(new Response<PlayerSessionStatsDto> { Status = 200, Message = "Player statistics retrieved successfully.", Data = stats });
        }

        [HttpGet("gamesessions/{sessionId}/roster")]
        public async Task<ActionResult<Response<IEnumerable<SessionRosterPlayerDto>>>> GetSessionRoster(int sessionId)
        {
            var roster = await _matchService.GetSessionRosterAsync(sessionId, GetCurrentUserId());
            if (roster == null)
            {
                return StatusCode(403, new Response<object> { Status = 403, Message = "You do not have permission to view this session's roster." });
            }
            return Ok(new Response<IEnumerable<SessionRosterPlayerDto>> { Status = 200, Message = "Session roster retrieved successfully.", Data = roster });
        }

        [HttpPut("gamesessions/{sessionId}/courts")]
        public async Task<ActionResult<Response<object>>> UpdateSessionCourts(int sessionId, [FromBody] UpdateCourtsDto dto)
        {
            var success = await _matchService.UpdateSessionCourtsAsync(sessionId, GetCurrentUserId(), dto);
            if (!success)
            {
                return StatusCode(403, new Response<object> { Status = 403, Message = "Session not found or you do not have permission to update it." });
            }
            return Ok(new Response<object> { Status = 200, Message = "Session courts updated successfully." });
        }

        [HttpPost("gamesessions/{sessionId}/staged-matches")]
        public async Task<ActionResult<Response<StagedMatchDto>>> CreateStagedMatch(int sessionId, [FromBody] CreateStagedMatchDto dto)
        {
            var stagedMatch = await _matchService.CreateStagedMatchAsync(sessionId, GetCurrentUserId(), dto);
            if (stagedMatch == null)
            {
                return StatusCode(403, new Response<object> { Status = 403, Message = "Failed to create staged match. Check permissions or player availability." });
            }
            return Ok(new Response<StagedMatchDto> { Status = 201, Message = "Staged match created successfully.", Data = stagedMatch });
        }

        [HttpPost("staged-matches/{matchId}/start")]
        public async Task<ActionResult<Response<CurrentlyPlayingMatchDto>>> StartStagedMatch(int matchId, [FromBody] StartStagedMatchDto dto)
        {
            var startedMatch = await _matchService.StartStagedMatchAsync(matchId, GetCurrentUserId(), dto);
            if (startedMatch == null)
            {
                return StatusCode(403, new Response<object> { Status = 403, Message = "Failed to start match. It may not be a valid staged match or you lack permissions." });
            }
            return Ok(new Response<CurrentlyPlayingMatchDto> { Status = 200, Message = "Match started from staged successfully.", Data = startedMatch });
        }

    }
}