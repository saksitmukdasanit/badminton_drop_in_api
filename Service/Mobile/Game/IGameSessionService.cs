using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IGameSessionService
    {
        Task<ManageGameSessionDto> CreateSessionAsync(int organizerUserId, SaveGameSessionDto dto);
        Task<ManageGameSessionDto?> GetSessionByIdAsync(int sessionId);
        Task<IEnumerable<GameSessionSummaryDto>> GetUpcomingSessionsAsync();
        Task<IEnumerable<GameSessionSummaryDto>> GetMyCreatedSessionsAsync(int organizerUserId);
        Task<ManageGameSessionDto?> UpdateSessionAsync(int sessionId, int organizerUserId, SaveGameSessionDto dto);
        Task<bool> CancelSessionAsync(int sessionId, int organizerUserId);
        Task<ManageGameSessionDto> DuplicateSessionForNextWeekAsync(int oldSessionId, int organizerUserId);
    }
}