namespace DropInBadAPI.Models;

public class AdminAuditLog
{
    public long AdminAuditLogId { get; set; }

    public int CmsAdminUserId { get; set; }

    public CmsAdminUser? AdminUser { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? DetailsJson { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
