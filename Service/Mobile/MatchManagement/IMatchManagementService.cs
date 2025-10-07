using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IMatchManagementService
    {
        Task<LiveSessionStateDto?> GetLiveStateAsync(int sessionId, int organizerUserId);
        Task<CurrentlyPlayingMatchDto> StartMatchAsync(int sessionId, int organizerUserId, CreateMatchDto dto);
        Task<bool> SubmitPlayerResultAsync(int matchId, int userId, SubmitResultDto dto);
        Task<BillSummaryDto?> CheckoutParticipantAsync(int participantId, string participantType, int organizerUserId);
    }
}