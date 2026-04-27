using System;

namespace DropInBadAPI.Dtos
{
    public record OrganizerDashboardDto
    {
        public OrganizerDashboardProfileDto Profile { get; init; } = new();
        public OrganizerDashboardStatsDto Stats { get; init; } = new();
        public UpcomingSessionCardDto? NextUpcomingSession { get; init; }
    }

    public record OrganizerDashboardProfileDto
    {
        public string Nickname { get; init; } = string.Empty;
        public string? ProfilePhotoUrl { get; init; }
        public byte Status { get; init; } // สถานะผู้จัด 0=Pending, 1=Approved
    }

    public record OrganizerDashboardStatsDto
    {
        public int TotalSessionsHosted { get; init; }
        public int TotalPlayersJoined { get; init; }
        public decimal TotalNetIncome { get; init; } // รายได้สุทธิ (หักค่าธรรมเนียมแล้ว)
        public decimal WalletBalance { get; init; } // เงินในกระเป๋าที่ถอนได้
        public decimal TotalPendingIncome { get; init; } // ยอดค้างชำระจากผู้เล่น
        public int FollowersCount { get; init; }
    }
}