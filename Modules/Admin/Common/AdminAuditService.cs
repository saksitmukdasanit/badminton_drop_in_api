using DropInBadAPI.Data;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public class AdminAuditService : IAdminAuditService
{
    private readonly BadmintonDbContext _db;

    public AdminAuditService(BadmintonDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(int cmsAdminUserId, string action, string? entityType, string? entityId, string? detailsJson, string? ipAddress)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            CmsAdminUserId = cmsAdminUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = detailsJson,
            IpAddress = ipAddress,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
