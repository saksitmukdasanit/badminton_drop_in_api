using DropInBadAPI.Dtos;
using DropInBadAPI.Models; // << เพิ่ม using สำหรับ Response<T>
using DropInBadAPI.Service.Mobile.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Mobile
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GameSessionsController : ControllerBase
    {
        private readonly IGameSessionService _sessionService;
        public GameSessionsController(IGameSessionService sessionService) { _sessionService = sessionService; }
        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET: api/GameSessions/upcoming
        [HttpGet("upcoming")]
        [AllowAnonymous]
        public async Task<ActionResult<Response<IEnumerable<GameSessionSummaryDto>>>> GetUpcomingSessions()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) != null
             ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
             : (int?)null;

            var sessions = await _sessionService.GetUpcomingSessionsAsync(currentUserId);
            var response = new Response<IEnumerable<UpcomingSessionCardDto>>
            {
                Status = 200,
                Message = "Upcoming sessions retrieved successfully.",
                Data = sessions
            };
            return Ok(response);
        }

        // GET: api/GameSessions/my-history
        [HttpGet("my-history")]
        public async Task<ActionResult<Response<IEnumerable<OrganizerGameSessionDto>>>> GetMyHistory()
        {
            var sessions = await _sessionService.GetMyPastSessionsAsync(GetCurrentUserId());
            var response = new Response<IEnumerable<OrganizerGameSessionDto>>
            {
                Status = 200,
                Message = "User's created sessions retrieved successfully.",
                Data = sessions
            };
            return Ok(response);
        }

        // GET: api/GameSessions/{id}/analytics
        [HttpGet("{id}/analytics")]
        public async Task<ActionResult<Response<GameSessionAnalyticsDto>>> GetSessionAnalytics(int id)
        {
            var analytics = await _sessionService.GetSessionAnalyticsAsync(id, GetCurrentUserId());
            if (analytics == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Session not found or you do not have permission." });
            }

            return Ok(new Response<GameSessionAnalyticsDto> { Status = 200, Message = "Session analytics retrieved successfully.", Data = analytics });
        }

        // GET: api/GameSessions/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Response<EditGameSessionDto>>> GetSession(int id)
        {
            var session = await _sessionService.GetSessionForEditAsync(id);
            if (session == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Session not found." });
            }

            return Ok(new Response<EditGameSessionDto> { Status = 200, Message = "Session retrieved successfully.", Data = session });
        }

        // POST: api/GameSessions
        [HttpPost]
        public async Task<ActionResult<Response<ManageGameSessionDto>>> CreateSession([FromBody] SaveGameSessionDto dto)
        {
            var newSession = await _sessionService.CreateSessionAsync(GetCurrentUserId(), dto);
            var response = new Response<ManageGameSessionDto>
            {
                Status = 201,
                Message = "Session created successfully.",
                Data = newSession
            };
            return CreatedAtAction(nameof(GetSession), new { id = newSession.SessionId }, response);
        }

        // POST: api/GameSessions/5/duplicate
        [HttpPost("{id}/duplicate")]
        public async Task<ActionResult<Response<ManageGameSessionDto>>> DuplicateSession(int id)
        {
            try
            {
                var duplicatedSession = await _sessionService.DuplicateSessionForNextWeekAsync(id, GetCurrentUserId());
                var response = new Response<ManageGameSessionDto>
                {
                    Status = 201,
                    Message = "Session duplicated successfully.",
                    Data = duplicatedSession
                };
                return CreatedAtAction(nameof(GetSession), new { id = duplicatedSession.SessionId }, response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new Response<object> { Status = 404, Message = ex.Message });
            }
        }

        // PUT: api/GameSessions/5
        [HttpPut("{id}")]
        public async Task<ActionResult<Response<ManageGameSessionDto>>> UpdateSession(int id, [FromBody] SaveGameSessionDto dto)
        {
            var updatedSession = await _sessionService.UpdateSessionAsync(id, GetCurrentUserId(), dto);
            if (updatedSession == null)
            {
                return StatusCode(403, new Response<object> { Status = 403, Message = "Session not found or you do not have permission to update it." });
            }

            return Ok(new Response<ManageGameSessionDto> { Status = 200, Message = "Session updated successfully.", Data = updatedSession });
        }

        // DELETE: api/GameSessions/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<Response<object>>> CancelSession(int id)
        {
            var success = await _sessionService.CancelSessionAsync(id, GetCurrentUserId());
            if (!success)
            {
                return StatusCode(403, new Response<object> { Status = 403, Message = "Session not found or you do not have permission to cancel it." });
            }

            return Ok(new Response<object> { Status = 200, Message = "Session has been cancelled." });
        }

        [HttpPut("{id}/cancel-by-organizer")]
        public async Task<ActionResult<Response<object>>> CancelSessionByOrganizer(int id)
        {
            var success = await _sessionService.CancelSessionByOrganizerAsync(id, GetCurrentUserId());

            if (!success)
            {
                return StatusCode(403, new Response<object> { Status = 403, Message = "Session not found or you do not have permission to cancel it." });
            }

            return Ok(new Response<object> { Status = 200, Message = "Session has been cancelled by the organizer." });
        }


        [HttpPost("{id}/join")]
        public async Task<ActionResult<Response<JoinSessionResponseDto>>> JoinSession(int id)
        {
            var (data, errorMessage) = await _sessionService.JoinSessionAsync(id, GetCurrentUserId());

            if (data == null)
            {
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }

            return Ok(new Response<JoinSessionResponseDto> { Status = 200, Message = data.StatusMessage, Data = data });
        }

        // DELETE: api/GameSessions/5/cancel
        [HttpDelete("{id}/cancel")]
        public async Task<ActionResult<Response<object>>> CancelBooking(int id)
        {
            var (success, errorMessage) = await _sessionService.CancelBookingAsync(id, GetCurrentUserId());

            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }

            return Ok(new Response<object> { Status = 200, Message = "Your booking has been cancelled." });
        }

        [HttpPost("{id}/add-guest")]
        public async Task<ActionResult<Response<ParticipantDto>>> AddGuestToSession(int id, [FromBody] AddGuestDto dto)
        {
            var (data, errorMessage) = await _sessionService.AddGuestAsync(id, GetCurrentUserId(), dto);

            if (data == null)
            {
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }

            return Ok(new Response<ParticipantDto> { Status = 200, Message = "Guest added successfully.", Data = data });
        }

        [HttpPut("{sessionId}/participants/{participantType}/{participantId}/skill-level")]
        public async Task<ActionResult<Response<object>>> UpdateParticipantSkillLevel(int sessionId, string participantType, int participantId, [FromBody] UpdateSkillLevelDto dto)
        {
            var (success, errorMessage) = await _sessionService.UpdateParticipantSkillLevelAsync(sessionId, participantType, participantId, dto.SkillLevelId, GetCurrentUserId());

            if (!success)
            {
                // สามารถใช้ NotFound หรือ BadRequest ได้ตามความเหมาะสมของ Error Message
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }

            return Ok(new Response<object> { Status = 200, Message = "Skill level updated successfully." });
        }


        [HttpGet("my-upcoming")]
        public async Task<ActionResult<Response<IEnumerable<UpcomingSessionCardDto>>>> GetMyUpcoming()
        {
            var sessions = await _sessionService.GetMyUpcomingSessionsAsync(GetCurrentUserId());
            return Ok(new Response<IEnumerable<UpcomingSessionCardDto>>
            {
                Status = 200,
                Message = "Organizer's upcoming sessions retrieved.",
                Data = sessions
            });
        }

        [HttpPost("{id}/start")]
        public async Task<ActionResult<Response<object>>> StartSession(int id)
        {
            var (success, errorMessage) = await _sessionService.StartSessionAsync(id, GetCurrentUserId());

            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }

            return Ok(new Response<object> { Status = 200, Message = "Session successfully started." });
        }
    }
}