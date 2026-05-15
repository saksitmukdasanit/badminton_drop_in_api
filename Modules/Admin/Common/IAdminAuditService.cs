namespace DropInBadAPI.Modules.Admin;

public interface IAdminAuditService
{
    Task LogAsync(int cmsAdminUserId, string action, string? entityType, string? entityId, string? detailsJson, string? ipAddress);
}
