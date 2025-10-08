using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models; // << เพิ่ม using สำหรับ Response<T>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers
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
            var sessions = await _sessionService.GetUpcomingSessionsAsync();
            var response = new Response<IEnumerable<GameSessionSummaryDto>>
            {
                Status = 200,
                Message = "Upcoming sessions retrieved successfully.",
                Data = sessions
            };
            return Ok(response);
        }

        // GET: api/GameSessions/my-history
        [HttpGet("my-history")]
        public async Task<ActionResult<Response<IEnumerable<GameSessionSummaryDto>>>> GetMyHistory()
        {
            var sessions = await _sessionService.GetMyCreatedSessionsAsync(GetCurrentUserId());
            var response = new Response<IEnumerable<GameSessionSummaryDto>>
            {
                Status = 200,
                Message = "User's created sessions retrieved successfully.",
                Data = sessions
            };
            return Ok(response);
        }

        // GET: api/GameSessions/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Response<ManageGameSessionDto>>> GetSession(int id)
        {
            var session = await _sessionService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "Session not found." });
            }
            
            return Ok(new Response<ManageGameSessionDto> { Status = 200, Message = "Session retrieved successfully.", Data = session });
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
    }
}