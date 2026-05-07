namespace DropInBadAPI.Modules.UserSafety;

public interface IUserSafetyService
{
    Task<(bool Success, string Message)> ReportUserAsync(int reporterUserId, ReportUserDto dto);
    Task<(bool Success, string Message)> BlockUserAsync(int blockerUserId, int blockedUserId);
    Task<(bool Success, string Message)> UnblockUserAsync(int blockerUserId, int blockedUserId);
    Task<List<BlockedUserItemDto>> GetBlockedUsersAsync(int blockerUserId);
}
