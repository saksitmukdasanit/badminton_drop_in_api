namespace DropInBadAPI.Modules.Admin;

public record AdminDashboardSummaryDto(
    int TotalActiveUsers,
    int TotalOrganizers,
    int TotalGameSessions,
    int SessionsCreatedToday,
    int NotificationsLast24h,
    int CmsContentItems,
    int CmsPolicyDocuments,
    AdminDashboardAlertDto Alerts,
    List<AdminDashboardDailyPointDto> Daily7Days,
    List<AdminDashboardDailyPointDto> Daily30Days);

public record AdminDashboardAlertDto(
    int PendingOrganizerApprovals,
    int AbnormalItems,
    int NotificationSendFailures);

public record AdminDashboardDailyPointDto(
    DateOnly Date,
    int NewUsers,
    int NewSessions,
    int NotificationsSent);

