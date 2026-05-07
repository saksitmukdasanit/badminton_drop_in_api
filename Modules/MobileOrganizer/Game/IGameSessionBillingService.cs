using DropInBadAPI.Dtos;

namespace DropInBadAPI.Service.Mobile.Game;

public interface IGameSessionBillingService
{
    Task<GameSessionFinancialsDto?> GetSessionFinancialsAsync(int sessionId, int organizerUserId);
}
