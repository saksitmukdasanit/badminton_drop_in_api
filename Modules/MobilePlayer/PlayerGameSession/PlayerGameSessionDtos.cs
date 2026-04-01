namespace DropInBadAPI.Dtos
{

    public class PlayerGameSessionViewDto
    {
        public int SessionId { get; set; }
        public string GroupName { get; set; }
        public int Status { get; set; }
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }

        // ข้อมูลสนาม
        public string VenueName { get; set; }
        public string VenueAddress { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // ข้อมูลผู้จัด
        public OrganizerInfoDto Organizer { get; set; }

        // ข้อมูลค่าใช้จ่ายและลูกแบด
        public string? ShuttlecockInfo { get; set; } // เช่น "YONEX - AEROSENSA 30"
        public decimal? ShuttlecockCostPerUnit { get; set; }
        public decimal? CourtFeePerPerson { get; set; }
        public int MaxParticipants { get; set; }
        public int CurrentParticipants { get; set; }

        // ข้อมูลอื่นๆ
        public string? Notes { get; set; }
        public List<string> PhotoUrls { get; set; } = new();
        public List<FacilityDto> Facilities { get; set; } = new();
        public List<ParticipantDto> Participants { get; set; } = new();

        // **สำคัญ:** สถานะของผู้ใช้ที่กำลังดูหน้านี้
        public string CurrentUserStatus { get; set; }
    }


    public class OrganizerInfoDto
    {
        public int UserId { get; set; }
        public string Nickname { get; set; }
        public string? ProfilePhotoUrl { get; set; }
    }


    public record JoinSessionResponseDto
    {
        public int ParticipantId { get; init; }
        public int Status { get; init; } // 1=เข้าร่วม, 2=รอคิว
        public string StatusMessage { get; init; } = string.Empty; // "เข้าร่วมสำเร็จ" หรือ "คุณอยู่ในคิวสำรอง"
    }

    public record FacilityDto(int FacilityId, string FacilityName, string IconUrl);

        // --- NEW: DTO สำหรับหน้า History Detail ---
        public class PlayerHistoryDetailDto
        {
            public HistorySummaryDto Summary { get; set; } = new();
            public HistoryPaymentDto Payment { get; set; } = new();
            public string UserStatus { get; set; } = string.Empty;
            public List<HistoryMatchDto> Matches { get; set; } = new();
        }

        public class HistorySummaryDto
        {
            public int TotalGames { get; set; }
            public int TotalShuttlecocks { get; set; }
            public int TotalPlayTime { get; set; }
            public int TotalWaitTime { get; set; }
        }

        public class HistoryPaymentDto
        {
            public string Status { get; set; } = string.Empty;
            public decimal CourtFee { get; set; }
            public decimal ServiceFee { get; set; }
            public decimal TotalAmount { get; set; }
            public string? PaymentDate { get; set; }
            public string? PaymentMethod { get; set; }
        }

        public class HistoryMatchDto
        {
            public int MatchId { get; set; }
            public int? Result { get; set; }
            public string? Notes { get; set; }
            public int DurationMinutes { get; set; }
            public string? CourtNumber { get; set; }
            public int ShuttlecocksUsed { get; set; }
            public List<PlayerInMatchDto> MyTeam { get; set; } = new();
            public List<PlayerInMatchDto> Opponents { get; set; } = new();
        }

        public class PlayerCheckinRequestDto
        {
            public string ScannedQrCode { get; set; } = string.Empty;
        }

        // --- NEW: DTOs for GamePlayerPage ---
        public class SubmitMatchResultDto
        {
            public int Result { get; set; } // 1=ชนะ, 2=แพ้, 3=เสมอ
            public string? Notes { get; set; }
        }

        public class PlayerBillPreviewDto
        {
            public List<BillLineItemDto> LineItems { get; set; } = new();
        }

        public class PlayerStatsDto
        {
            public int TotalGamesPlayed { get; set; }
            public string TotalMinutesPlayed { get; set; } = "0";
            public int Wins { get; set; }
            public int Losses { get; set; }
            public List<PlayerMatchHistoryItemDto> MatchHistory { get; set; } = new();
        }

        public class PlayerMatchHistoryItemDto : HistoryMatchDto
        {
            // สืบทอดตัวเดิมมาใช้ซ้ำ แล้วเพิ่ม Teammate เข้าไป
            public PlayerInMatchDto Teammate { get; set; } = new();
        }

    public class CustomLineItemDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class PlayerPaymentRequestDto
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public List<CustomLineItemDto>? CustomItems { get; set; }
    }
}