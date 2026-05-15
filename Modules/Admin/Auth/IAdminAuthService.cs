namespace DropInBadAPI.Modules.Admin;

public interface IAdminAuthService
{
    Task<(AdminTokenResponseDto? Data, string Error)> LoginAsync(AdminLoginDto dto, string? ipAddress);

    Task<(AdminTokenResponseDto? Data, string Error)> RefreshAsync(AdminRefreshDto dto);

    Task<(bool Ok, string Error)> LogoutAsync(int cmsAdminUserId);
}
