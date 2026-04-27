using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IMatchManagementService
    {
        Task<LiveSessionStateDto?> GetLiveStateAsync(int sessionId, int organizerUserId);
        Task<CurrentlyPlayingMatchDto> StartMatchAsync(int sessionId, int organizerUserId, CreateMatchDto dto);
        Task<bool> EndMatchAsync(int matchId, int organizerUserId);
        Task<BillSummaryDto?> GetParticipantBillPreviewAsync(string participantType, int participantId, int organizerUserId);
        Task<BillSummaryDto?> CheckoutParticipantAsync(string participantType, int participantId, int organizerUserId, CheckoutRequestDto? customCheckout = null);
        Task<(bool Success, string Message, string? QrCodeStr)> PayBillAsync(int billId, int organizerUserId, PaymentRequestDto dto); // --- NEW ---
        Task<(bool Success, string Message)> CheckinParticipantAsync(int sessionId, int organizerUserId, CheckinDto dto);
        Task<WaitingPlayerDto> AddWalkinGuestAsync(int sessionId, int organizerUserId, AddWalkinDto dto);
        Task<List<GuestSuggestionDto>> SearchPreviousGuestsAsync(int organizerUserId, string? query);
        Task<bool> UpdateParticipantSkillAsync(string participantType, int participantId, UpdateParticipantSkillDto dto);
        Task<PlayerSessionStatsDto?> GetPlayerSessionStatsAsync(int sessionId, string participantType, int participantId);
        Task<IEnumerable<SessionRosterPlayerDto>?> GetSessionRosterAsync(int sessionId, int organizerUserId);
        Task<bool> UpdateSessionCourtsAsync(int sessionId, int organizerUserId, UpdateCourtsDto dto);
        Task<StagedMatchDto?> CreateStagedMatchAsync(int sessionId, int organizerUserId, CreateStagedMatchDto dto);
        Task<CurrentlyPlayingMatchDto?> StartStagedMatchAsync(int matchId, int organizerUserId, StartStagedMatchDto dto);
        Task<bool> CancelBillAsync(int billId, int organizerUserId);
        Task<OrganizerFinanceDashboardDto> GetFinanceDashboardAsync(int organizerUserId);
        Task<(bool Success, string Message)> WithdrawOrganizerFundsAsync(int organizerUserId, decimal amount);

        Task<bool> ProcessQrPaymentWebhookAsync(string referenceId, decimal amount);

    }
}