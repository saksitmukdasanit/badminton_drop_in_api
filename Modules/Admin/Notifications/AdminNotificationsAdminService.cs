using DropInBadAPI.Data;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public interface IAdminNotificationsAdminService
{
    Task<(List<NotificationAdminListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize);
    Task<NotificationAdminListItemDto?> GetByIdAsync(int notificationId);
    Task<(NotificationAdminListItemDto? Data, string Error)> CreateAndSendAsync(NotificationAdminCreateDto dto);
    Task<(bool Ok, string Error)> DeleteAsync(int notificationId);
}

public class AdminNotificationsAdminService : IAdminNotificationsAdminService
{
    private readonly BadmintonDbContext _db;
    private readonly INotificationService _notify;

    public AdminNotificationsAdminService(BadmintonDbContext db, INotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    public async Task<(List<NotificationAdminListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q =
            from n in _db.Notifications.AsNoTracking()
            join u in _db.Users.AsNoTracking() on n.UserId equals u.UserId
            join p in _db.UserProfiles.AsNoTracking() on u.UserId equals p.UserId into pj
            from p in pj.DefaultIfEmpty()
            select new { n, p };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.n.Title, pattern)
                || EF.Functions.ILike(x.n.Message, pattern)
                || (x.p != null && EF.Functions.ILike(x.p.Nickname ?? "", pattern))
                || (x.p != null && EF.Functions.ILike(x.p.PrimaryContactEmail ?? "", pattern)));
        }

        var total = await q.LongCountAsync();
        var rows = await q
            .OrderByDescending(x => x.n.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationAdminListItemDto(
                x.n.NotificationId,
                x.n.UserId,
                x.p != null ? (x.p.Nickname ?? x.p.PrimaryContactEmail ?? "") : "",
                x.n.Title,
                x.n.Message,
                x.n.Type,
                x.n.ReferenceId,
                x.n.IsRead,
                x.n.CreatedDate))
            .ToListAsync();

        var items = rows
            .Select(r => r with
            {
                UserNicknameOrEmail = string.IsNullOrWhiteSpace(r.UserNicknameOrEmail)
                    ? $"#{r.UserId}"
                    : r.UserNicknameOrEmail
            })
            .ToList();

        return (items, total);
    }

    public async Task<NotificationAdminListItemDto?> GetByIdAsync(int notificationId)
    {
        var x = await (
            from n in _db.Notifications.AsNoTracking()
            join u in _db.Users.AsNoTracking() on n.UserId equals u.UserId
            join p in _db.UserProfiles.AsNoTracking() on u.UserId equals p.UserId into pj
            from p in pj.DefaultIfEmpty()
            where n.NotificationId == notificationId
            select new { n, p }).FirstOrDefaultAsync();

        if (x == null)
        {
            return null;
        }

        var label = x.p != null
            ? (x.p.Nickname ?? x.p.PrimaryContactEmail ?? "#" + x.n.UserId.ToString())
            : ("#" + x.n.UserId.ToString());

        return new NotificationAdminListItemDto(
            x.n.NotificationId,
            x.n.UserId,
            label,
            x.n.Title,
            x.n.Message,
            x.n.Type,
            x.n.ReferenceId,
            x.n.IsRead,
            x.n.CreatedDate);
    }

    public async Task<(NotificationAdminListItemDto? Data, string Error)> CreateAndSendAsync(NotificationAdminCreateDto dto)
    {
        if (!await _db.Users.AnyAsync(u => u.UserId == dto.UserId && u.DeletedAt == null))
        {
            return (null, "ไม่พบผู้ใช้");
        }

        var title = dto.Title.Trim();
        var msg = dto.Message.Trim();
        var type = string.IsNullOrWhiteSpace(dto.Type) ? "cms.admin" : dto.Type.Trim();

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(msg))
        {
            return (null, "หัวข้อและข้อความจำเป็น");
        }

        var entity = new Notification
        {
            UserId = dto.UserId,
            Title = title,
            Message = msg,
            Type = type,
            ReferenceId = dto.ReferenceId,
            IsRead = false,
            CreatedDate = DateTime.UtcNow
        };
        _db.Notifications.Add(entity);
        await _db.SaveChangesAsync();

        try
        {
            await _notify.DispatchFirebaseForUserAsync(dto.UserId, title, msg, type, dto.ReferenceId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CMS-NOTIFY] FCM error UserId {dto.UserId}: {ex.Message}");
        }

        return (await GetByIdAsync(entity.NotificationId), string.Empty);
    }

    public async Task<(bool Ok, string Error)> DeleteAsync(int notificationId)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(a => a.NotificationId == notificationId);
        if (n == null)
        {
            return (false, "ไม่พบแจ้งเตือน");
        }

        _db.Notifications.Remove(n);
        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }
}
