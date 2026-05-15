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
        public string? LatestSkillLevelName { get; init; } // ระดับมือที่แสดงบนหน้าแรก (ตามการตั้งค่าหรือล่าสุด)
        /// <summary>ผู้จัดที่เลือกให้แสดงบนหน้าแรก — null = โหมดอัตโนมัติ (ล่าสุด)</summary>
        public int? SkillDisplayOrganizerUserId { get; init; }
        /// <summary>true = ใช้ระดับจากผู้จัดที่เลือก, false = ระบบเลือกจากอัปเดตล่าสุด</summary>
        public bool SkillLevelUsesManualOrganizerPreference { get; init; }
    }

    public record PlayerOrganizerSkillItemDto
    {
        public int OrganizerUserId { get; init; }
        public string OrganizerNickname { get; init; } = string.Empty;
        public string? OrganizerProfilePhotoUrl { get; init; }
        public int SkillLevelId { get; init; }
        public string SkillLevelName { get; init; } = string.Empty;
        public DateTime? UpdatedDateUtc { get; init; }
        public bool IsPreferredForHome { get; init; }
    }

    public record SetPlayerSkillDisplayOrganizerRequestDto
    {
        /// <summary>null = ใช้โหมดอัตโนมัติ (แสดงจากแถวล่าสุด)</summary>
        public int? OrganizerUserId { get; init; }
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