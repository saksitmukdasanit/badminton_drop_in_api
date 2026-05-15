namespace DropInBadAPI.Modules.Admin;

/// <summary>รายการแอดมิน CMS (ไม่มีรหัสผ่าน / refresh token)</summary>
public record CmsAdminUserListItemDto(
    int CmsAdminUserId,
    string Email,
    string? DisplayName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record CmsAdminUserCreateDto(string Email, string Password, string? DisplayName);

public record CmsAdminUserUpdateDto(string DisplayName, bool IsActive, string? NewPassword);

