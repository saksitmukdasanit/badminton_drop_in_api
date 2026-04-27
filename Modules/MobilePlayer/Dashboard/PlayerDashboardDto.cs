using System;

namespace DropInBadAPI.Dtos
{
    public record PlayerDashboardDto
    {
        public PlayerDashboardProfileDto Profile { get; init; } = new();
        public PlayerDashboardStatsDto Stats { get; init; } = new();
        public UpcomingSessionCardDto? NextUpcomingSession { get; init; }
    }

    public record PlayerDashboardProfileDto
    {
        public string Nickname { get; init; } = string.Empty;
        public string? ProfilePhotoUrl { get; init; }
        public string? LatestSkillLevelName { get; init; } // ระดับมือล่าสุด
    }

    public record PlayerDashboardStatsDto
    {
        public int TotalMatches { get; init; }
        public int TotalPlayTimeMinutes { get; init; }
        public decimal TotalSpent { get; init; }
        public int CancelCount { get; init; }
        public int FollowingCount { get; init; }
        public int TotalWins { get; init; }
        public decimal UnpaidBalance { get; init; }
        public decimal WalletBalance { get; init; }
    }

    public record DashboardDto
    {
        public int NotificationId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? ReferenceId { get; init; }
        public bool IsRead { get; init; }
        public DateTime CreatedDate { get; init; }
    }
}