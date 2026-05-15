namespace DropInBadAPI.Models;

/// <summary>บัญชีแอดมิน CMS (แยกจากผู้ใช้แอปมือถือ)</summary>
public class CmsAdminUser
{
    public int CmsAdminUserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public bool IsActive { get; set; } = true;

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiryUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
