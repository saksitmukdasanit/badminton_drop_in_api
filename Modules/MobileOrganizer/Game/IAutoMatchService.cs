using DropInBadAPI.Dtos;

namespace DropInBadAPI.Service.Mobile.Game;

/// <summary>
/// Service เฉพาะ logic จัดคู่อัตโนมัติและการจัด/ย้ายผู้เล่นในสนาม Staged
/// แยกออกมาจาก <c>GameSessionService</c> เพื่อให้ test/scale ง่าย
/// </summary>
public interface IAutoMatchService
{
    Task<(bool Success, string ErrorMessage)> AutoMatchAsync(int sessionId, int organizerUserId, AutoMatchRequestDto dto);
    Task<(bool Success, string ErrorMessage)> SwapPlayersAsync(int sessionId, int organizerUserId, SwapPlayersRequestDto dto);
    Task<(bool Success, string ErrorMessage)> AssignReserveToCourtAsync(int sessionId, int organizerUserId, AssignReserveRequestDto dto);
    Task<(bool Success, string ErrorMessage)> MovePlayersAsync(int sessionId, int organizerUserId, MovePlayersRequestDto dto);
}
