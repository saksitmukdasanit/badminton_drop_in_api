namespace DropInBadAPI.Modules.Admin;

public record AdminLoginDto(string Email, string Password);

public record AdminTokenResponseDto(string AccessToken, string RefreshToken, int AdminUserId, string Email, string? DisplayName);

public record AdminRefreshDto(string AccessToken, string RefreshToken);

