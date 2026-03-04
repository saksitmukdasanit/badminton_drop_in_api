using DropInBadAPI.Dtos;

namespace DropInBadAPI.Service.Mobile.Game
{
    public interface IGameSessionService
    {        Task<ManageGameSessionDto> CreateSessionAsync(int organizerUserId, SaveGameSessionDto dto);
        Task<EditGameSessionDto?> GetSessionForEditAsync(int sessionId);
        Task<IEnumerable<UpcomingSessionCardDto>> GetUpcomingSessionsAsync(int? currentUserId);
        Task<ManageGameSessionDto?> UpdateSessionAsync(int sessionId, int organizerUserId, SaveGameSessionDto dto);
        Task<bool> CancelSessionAsync(int sessionId, int organizerUserId);
        Task<bool> CancelSessionByOrganizerAsync(int sessionId, int organizerUserId);
        Task<ManageGameSessionDto> DuplicateSessionForNextWeekAsync(int oldSessionId, int organizerUserId);
        Task<PlayerGameSessionViewDto?> GetSessionForPlayerViewAsync(int sessionId, int? currentUserId);
        Task<(JoinSessionResponseDto? Data, string ErrorMessage)> JoinSessionAsync(int sessionId, int userId);
        Task<(bool Success, string ErrorMessage)> CancelBookingAsync(int sessionId, int userId);
        Task<IEnumerable<UpcomingSessionCardDto>> GetMyUpcomingSessionsAsync(int organizerUserId);
        Task<(ParticipantDto? Data, string ErrorMessage)> AddGuestAsync(int sessionId, int organizerUserId, AddGuestDto dto);
        Task<(bool Success, string ErrorMessage)> UpdateParticipantSkillLevelAsync(int sessionId, string participantType, int participantId, int? newSkillLevelId, int organizerUserId);
        Task<IEnumerable<OrganizerGameSessionDto>> GetMyPastSessionsAsync(int organizerUserId);
        Task<(bool Success, string ErrorMessage)> StartSessionAsync(int sessionId, int organizerUserId);
        Task<GameSessionAnalyticsDto?> GetSessionAnalyticsAsync(int sessionId, int organizerUserId);
        Task<GameSessionFinancialsDto?> GetSessionFinancialsAsync(int sessionId, int organizerUserId);
        Task<bool> StartCompetitionAsync(int sessionId, int organizerUserId);
        Task<bool> EndCompetitionAsync(int sessionId, int organizerUserId);
        Task<(bool Success, string ErrorMessage)> RemoveParticipantAsync(int sessionId, string participantType, int participantId, int organizerUserId);
        Task<(bool Success, string ErrorMessage)> PromoteWaitlistedParticipantAsync(int sessionId, string participantType, int participantId, int organizerUserId);
        Task<(bool Success, string ErrorMessage)> AutoMatchAsync(int sessionId, int organizerUserId, AutoMatchRequestDto dto);
        Task<(bool Success, string ErrorMessage)> SwapPlayersAsync(int sessionId, int organizerUserId, SwapPlayersRequestDto dto);
        Task<(bool Success, string ErrorMessage)> AssignReserveToCourtAsync(int sessionId, int organizerUserId, AssignReserveRequestDto dto);
        Task<(bool Success, string ErrorMessage)> MovePlayersAsync(int sessionId, int organizerUserId, MovePlayersRequestDto dto);
    }
}