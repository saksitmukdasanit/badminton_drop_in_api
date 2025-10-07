using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
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
        [AllowAnonymous] // อนุญาตให้ทุกคนเห็นก๊วนที่กำลังจะมาถึง
        public async Task<IActionResult> GetUpcomingSessions()
        {
            var sessions = await _sessionService.GetUpcomingSessionsAsync();
            return Ok(sessions);
        }

        // GET: api/GameSessions/my-history
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyHistory()
        {
            var sessions = await _sessionService.GetMyCreatedSessionsAsync(GetCurrentUserId());
            return Ok(sessions);
        }

        // GET: api/GameSessions/5
        [HttpGet("{id}")]
        [AllowAnonymous] 
        public async Task<ActionResult<ManageGameSessionDto>> GetSession(int id)
        {
            var session = await _sessionService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }
            return Ok(session);
        }

        // POST: api/GameSessions
        [HttpPost]
        public async Task<IActionResult> CreateSession([FromBody] SaveGameSessionDto dto)
        {
            var newSession = await _sessionService.CreateSessionAsync(GetCurrentUserId(), dto);
            return CreatedAtAction(nameof(GetSession), new { id = newSession.SessionId }, newSession);
        }

        // POST: api/GameSessions/5/duplicate
        [HttpPost("{id}/duplicate")]
        public async Task<IActionResult> DuplicateSession(int id)
        {
            try
            {
                var duplicatedSession = await _sessionService.DuplicateSessionForNextWeekAsync(id, GetCurrentUserId());
                return CreatedAtAction(nameof(GetSession), new { id = duplicatedSession.SessionId }, duplicatedSession);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        // PUT: api/GameSessions/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSession(int id, [FromBody] SaveGameSessionDto dto)
        {
            var updatedSession = await _sessionService.UpdateSessionAsync(id, GetCurrentUserId(), dto);
            if (updatedSession == null) return Forbid(); // Forbid = ไม่มีสิทธิ์แก้ไข หรือ Not Found
            return Ok(updatedSession);
        }

        // DELETE: api/GameSessions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelSession(int id)
        {
            var success = await _sessionService.CancelSessionAsync(id, GetCurrentUserId());
            if (!success) return Forbid();
            return NoContent();
        }
    }
}