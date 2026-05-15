using DropInBadAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DropInBadAPI.Modules.Admin;

public interface IAdminDashboardService
{
    Task<AdminDashboardSummaryDto> GetSummaryAsync();
}

public class AdminDashboardService : IAdminDashboardService
{
    private readonly BadmintonDbContext _db;
    private readonly ILogger<AdminDashboardService> _logger;

    public AdminDashboardService(BadmintonDbContext db, ILogger<AdminDashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AdminDashboardSummaryDto> GetSummaryAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since = DateTime.UtcNow.AddHours(-24);

        var totalActiveUsers = await SafeCountAsync(() => _db.Users.LongCountAsync(u => u.IsActive && u.DeletedAt == null));
        var totalOrganizers = await SafeCountAsync(() => _db.OrganizerProfiles.LongCountAsync());
        var totalGameSessions = await SafeCountAsync(() => _db.GameSessions.LongCountAsync());
        var sessionsToday = await SafeCountAsync(() => _db.GameSessions.LongCountAsync(s => s.SessionDate == today));
        var notificationsLast24h = await SafeCountAsync(() => _db.Notifications.LongCountAsync(n => n.CreatedDate >= since));
        var cmsContentItems = await SafeCountAsync(() => _db.CmsContentItems.LongCountAsync());
        var cmsPolicyDocs = await SafeCountAsync(() => _db.CmsPolicyDocuments.LongCountAsync());
        var pendingOrganizerApprovals = await SafeCountAsync(() => _db.OrganizerProfiles.LongCountAsync(o => o.Status == 0));
        var abnormalUsers = await SafeCountAsync(() => _db.UserProfiles.LongCountAsync(p =>
            p.UserId <= 0
            || p.Nickname == null
            || p.Nickname == ""));
        var abnormalWallets = await SafeCountAsync(() => _db.UserWallets.LongCountAsync(w => w.Balance < 0));
        var abnormalOrganizers = await SafeCountAsync(() => _db.OrganizerProfiles.LongCountAsync(o =>
            o.BankAccountNumber == null
            || o.BankAccountNumber == ""));
        // Current data model has no persisted delivery failure log, so estimate risk by users without FCM token.
        var notificationSendFailures = await EstimateNotificationFailuresAsync();

        var daily7 = await BuildDailyTrendAsync(7);
        var daily30 = await BuildDailyTrendAsync(30);

        return new AdminDashboardSummaryDto(
            (int)Math.Min(int.MaxValue, totalActiveUsers),
            (int)Math.Min(int.MaxValue, totalOrganizers),
            (int)Math.Min(int.MaxValue, totalGameSessions),
            (int)Math.Min(int.MaxValue, sessionsToday),
            (int)Math.Min(int.MaxValue, notificationsLast24h),
            (int)Math.Min(int.MaxValue, cmsContentItems),
            (int)Math.Min(int.MaxValue, cmsPolicyDocs),
            new AdminDashboardAlertDto(
                (int)Math.Min(int.MaxValue, pendingOrganizerApprovals),
                (int)Math.Min(int.MaxValue, abnormalUsers + abnormalWallets + abnormalOrganizers),
                (int)Math.Min(int.MaxValue, notificationSendFailures)),
            daily7,
            daily30);
    }

    private async Task<List<AdminDashboardDailyPointDto>> BuildDailyTrendAsync(int days)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-(days - 1));
        var startDateTime = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var users = await SafeLoadUserDatesAsync(startDateTime);
        var sessions = await SafeLoadSessionDatesAsync(start);
        var notifications = await SafeLoadNotificationDatesAsync(startDateTime);

        var usersByDate = users
            .GroupBy(d => DateOnly.FromDateTime(d))
            .ToDictionary(g => g.Key, g => g.Count());
        var sessionsByDate = sessions
            .GroupBy(d => d)
            .ToDictionary(g => g.Key, g => g.Count());
        var notificationsByDate = notifications
            .GroupBy(d => DateOnly.FromDateTime(d))
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<AdminDashboardDailyPointDto>();
        for (var i = 0; i < days; i++)
        {
            var date = start.AddDays(i);
            result.Add(new AdminDashboardDailyPointDto(
                date,
                usersByDate.TryGetValue(date, out var u) ? u : 0,
                sessionsByDate.TryGetValue(date, out var s) ? s : 0,
                notificationsByDate.TryGetValue(date, out var n) ? n : 0));
        }
        return result;
    }

    private async Task<List<DateTime>> SafeLoadUserDatesAsync(DateTime startDateTime)
    {
        try
        {
            return await _db.Users.AsNoTracking()
                .Where(u => u.CreatedDate >= startDateTime)
                .Select(u => u.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard daily trend: failed loading new users from {StartDateTime}.", startDateTime);
            return new List<DateTime>();
        }
    }

    private async Task<List<DateOnly>> SafeLoadSessionDatesAsync(DateOnly startDate)
    {
        try
        {
            return await _db.GameSessions.AsNoTracking()
                .Where(s => s.SessionDate >= startDate)
                .Select(s => s.SessionDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard daily trend: failed loading sessions from {StartDate}.", startDate);
            return new List<DateOnly>();
        }
    }

    private async Task<List<DateTime>> SafeLoadNotificationDatesAsync(DateTime startDateTime)
    {
        try
        {
            return await _db.Notifications.AsNoTracking()
                .Where(n => n.CreatedDate >= startDateTime)
                .Select(n => n.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard daily trend: failed loading notifications from {StartDateTime}.", startDateTime);
            return new List<DateTime>();
        }
    }

    private async Task<long> SafeCountAsync(Func<Task<long>> query)
    {
        try
        {
            return await query();
        }
        catch
        {
            return 0;
        }
    }

    private async Task<int> EstimateNotificationFailuresAsync()
    {
        try
        {
            var activeUserIds = await _db.Users.AsNoTracking()
                .Where(u => u.DeletedAt == null)
                .Select(u => u.UserId)
                .ToListAsync();
            var usersWithTokenCount = await _db.UserFcmTokens.AsNoTracking()
                .Where(t => activeUserIds.Contains(t.UserId))
                .Select(t => t.UserId)
                .Distinct()
                .LongCountAsync();
            return Math.Max(0, activeUserIds.Count - (int)Math.Min(int.MaxValue, usersWithTokenCount));
        }
        catch
        {
            return 0;
        }
    }
}
