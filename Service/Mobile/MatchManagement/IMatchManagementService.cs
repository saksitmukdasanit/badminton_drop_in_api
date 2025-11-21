using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IMatchManagementService
    {
        Task<LiveSessionStateDto?> GetLiveStateAsync(int sessionId, int organizerUserId);
        Task<CurrentlyPlayingMatchDto> StartMatchAsync(int sessionId, int organizerUserId, CreateMatchDto dto);
        Task<bool> EndMatchAsync(int matchId, int organizerUserId);
        Task<bool> SubmitPlayerResultAsync(int matchId, int userId, SubmitResultDto dto);
        Task<BillSummaryDto?> CheckoutParticipantAsync(string participantType, int participantId, int organizerUserId);
        Task<(bool Success, string Message)> CheckinParticipantAsync(int sessionId, CheckinDto dto);
        Task<WaitingPlayerDto> AddWalkinGuestAsync(int sessionId, AddWalkinDto dto);
        Task<bool> UpdateParticipantSkillAsync(string participantType, int participantId, UpdateParticipantSkillDto dto);
        Task<PlayerSessionStatsDto?> GetPlayerSessionStatsAsync(int sessionId, string participantType, int participantId);
        Task<IEnumerable<SessionRosterPlayerDto>?> GetSessionRosterAsync(int sessionId, int organizerUserId);
        Task<bool> UpdateSessionCourtsAsync(int sessionId, int organizerUserId, UpdateCourtsDto dto);
        Task<StagedMatchDto?> CreateStagedMatchAsync(int sessionId, int organizerUserId, CreateStagedMatchDto dto);
        Task<CurrentlyPlayingMatchDto?> StartStagedMatchAsync(int matchId, int organizerUserId, StartStagedMatchDto dto);
    }
}