using DropInBadAPI.Dtos;

namespace DropInBadAPI.Service.MobilePlayer.Game
{
    public interface IPlayerGameSessionService
    {
        Task<IEnumerable<UpcomingSessionCardDto>> GetUpcomingSessionsAsync(int? currentUserId, string? keyword = null, string? sortBy = null, int? organizerId = null, List<DayOfWeek>? daysOfWeek = null, List<int>? gameTypeIds = null, int page = 1, int limit = 10);
        Task<MyGameSessionsResponseDto> GetMySessionsAsync(int userId);
        Task<IEnumerable<UpcomingSessionCardDto>> GetHistorySessionsAsync(int userId, string? keyword = null, string? sortBy = null, int page = 1, int limit = 10);
        Task<PlayerGameSessionViewDto?> GetSessionForPlayerViewAsync(int sessionId, int? currentUserId);
        Task<PlayerHistoryDetailDto?> GetHistoryDetailAsync(int sessionId, int userId);
        Task<(JoinSessionResponseDto? Data, string ErrorMessage)> JoinSessionAsync(int sessionId, int userId, PlayerJoinSessionRequestDto dto);
        Task<(bool Success, string ErrorMessage)> CancelBookingAsync(int sessionId, int userId, bool isAbort = false);
        Task<(bool Success, string ErrorMessage)> PlayerCheckinAsync(int sessionId, int userId, string scannedQrCode);
        Task<PlayerBillPreviewDto?> GetMyBillPreviewAsync(int sessionId, int userId);
        Task<PlayerStatsDto?> GetMyStatsAsync(int sessionId, int userId);
        Task<(bool Success, string ErrorMessage)> SubmitMatchResultAsync(int matchId, int userId, SubmitMatchResultDto dto);
        Task<(bool Success, string Message, string? QrCodeStr, int? BillId)> CheckoutAndPayAsync(int sessionId, int userId, PlayerPaymentRequestDto dto);
        Task<(bool Success, string ErrorMessage)> TogglePauseAsync(int sessionId, int userId, bool isPaused);
        Task<(bool Success, string ErrorMessage)> ToggleBookmarkAsync(int sessionId, int userId, bool isBookmark);
        Task<OrganizerSummaryDto?> GetOrganizerSummaryAsync(int organizerId, int? currentUserId);
        Task<IEnumerable<UpcomingSessionCardDto>> GetBookmarkedSessionsAsync(int userId);
    }
}