namespace DropInBadAPI.Dtos
{
    public record VenueDataDto(
        string GooglePlaceId,
        string Name,
        string Address,
        decimal Latitude,
        decimal Longitude
        );


    // DTO สำหรับรับข้อมูลตอนสร้างและแก้ไขก๊วน
    public record SaveGameSessionDto(
        string GroupName,
        VenueDataDto VenueData,
         DateOnly SessionDate,
        TimeOnly StartTime,
        TimeOnly EndTime,
        int? GameTypeId,
        int? PairingMethodId,
        int MaxParticipants,
        int? CostingMethod,
        decimal? CourtFeePerPerson,
        decimal? ShuttlecockFeePerPerson,
        decimal? TotalCourtCost,
        decimal? ShuttlecockCostPerUnit,
        int? ShuttlecockModelId,
        int? NumberOfCourts,
        string? CourtNumbers,
        string? Notes,
        List<int> FacilityIds,
        List<string> PhotoUrls
    );

    // DTO สำหรับแสดงข้อมูลก๊วนในหน้ารวม (List View)
    public record GameSessionSummaryDto
    {
        public int SessionId { get; set; }
        public string? GroupName { get; set; }
        public DateTime SessionStart { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public int CurrentParticipants { get; set; }
        public int MaxParticipants { get; set; }
    }

    // DTO สำหรับแสดงข้อมูลก๊วนแบบละเอียด (Detail View)
    public record GameSessionDetailDto
    {
        public int SessionId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int VenueId { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int MaxParticipants { get; set; }
        public int Status { get; set; }
        public int CreatedByUserId { get; set; }
        public List<string> PhotoUrls { get; set; } = new();
        public List<FacilityDto> Facilities { get; set; } = new();
    }

    public record EditGameSessionDto
    {
        public int SessionId { get; set; }
        public string GroupName { get; set; }
        public VenueDataDto VenueData { get; set; }
        public DateOnly SessionDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int? GameTypeId { get; set; }
        public int? PairingMethodId { get; set; }
        public int MaxParticipants { get; set; }
        public int? CostingMethod { get; set; }
        public decimal? CourtFeePerPerson { get; set; }
        public decimal? ShuttlecockFeePerPerson { get; set; }
        public decimal? TotalCourtCost { get; set; }
        public decimal? ShuttlecockCostPerUnit { get; set; }
        public int? ShuttlecockModelId { get; set; }
        public int? ShuttlecockBrandId { get; set; }
        public string? ShuttlecockBrandName { get; set; }
        public string? ShuttlecockModelName { get; set; }
        public string? GameTypeName { get; set; }
        public int CurrentParticipants { get; set; }
        public int? NumberOfCourts { get; set; }
        public string? CourtNumbers { get; set; }
        public string? Notes { get; set; }
        public List<int> FacilityIds { get; set; }
        public List<string> PhotoUrls { get; set; }
        public List<ParticipantDto> Participants { get; set; } = new();
        public int Status { get; set; }

    }

    public class ManageGameSessionDto
    {
        public int SessionId { get; set; }
        public string? GroupName { get; set; }
        public int Status { get; set; } // สถานะก๊วน (สำคัญมากสำหรับ Frontend)
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }

        // ข้อมูลสนาม
        public string? VenueName { get; set; }
        public string? VenueAddress { get; set; }

        // ข้อมูลค่าใช้จ่ายและลูกแบด
        public string? ShuttlecockBrandName { get; set; }
        public string? ShuttlecockModelName { get; set; }
        public decimal? ShuttlecockCostPerUnit { get; set; }
        public decimal? CourtFeePerPerson { get; set; }
        public int MaxParticipants { get; set; }
        public int CurrentParticipants { get; set; }
        public string? GameTypeName { get; set; } // เพิ่ม GameTypeName

        // ข้อมูลอื่นๆ
        public string? Notes { get; set; }
        public List<string> PhotoUrls { get; set; } = new();

        // รายชื่อผู้เข้าร่วมทั้งหมด (ทั้งสมาชิกและ Walk-in)
        public List<ParticipantDto> Participants { get; set; } = new();
    }

    public class OrganizerGameSessionDto
    {
        public int GameSessionId { get; set; }
        public DateTime Date { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public decimal TotalIncome { get; set; } // รายได้รวม
        public decimal PaidAmount { get; set; }  // จ่ายแล้ว
        public decimal UnpaidAmount { get; set; } // ค้างจ่าย
        public string Status { get; set; } = string.Empty; // สถานะก๊วน (เช่น จบแล้ว, กำลังจะถึง)
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int TotalParticipants { get; set; }
        public int? TotalCourts { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class GameSessionAnalyticsDto
    {
        public string GroupName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int TotalGames { get; set; }
        public int TotalShuttlecocks { get; set; } // จำนวนลูกที่ใช้ทั้งหมด
        public string TotalPlayTimeStart { get; set; } = string.Empty; // เวลาเริ่มตีทั้งหมด (HH:mm)
        public string TotalPlayTimeEnd { get; set; } = string.Empty;   // เวลาสิ้นสุดการตีทั้งหมด (HH:mm)
        public string AveragePlayTimePerGame { get; set; } = string.Empty; // เวลาตีต่อเกมเฉลี่ย (mm:ss)

        public MatchPerformanceDto? LongestGame { get; set; }
        public MatchPerformanceDto? ShortestGame { get; set; }

        public List<MatchHistoryDto> MatchHistory { get; set; } = new();
    }

    public class GameSessionFinancialsDto
    {
        public int SessionId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int CurrentParticipants { get; set; }
        public decimal CourtFeePerPerson { get; set; }
        public decimal ShuttlecockFeePerPerson { get; set; }
        public decimal ShuttlecockCostPerUnit { get; set; }
        public decimal TotalCourtCost { get; set; }
        public decimal TotalCourtIncome { get; set; }
        public decimal TotalShuttlecockFee { get; set; }
        public decimal TotalShuttlecockCost { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal TotalCashAmount { get; set; }
        public decimal TotalTransferAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
        public int TotalShuttlecocks { get; set; }

        // From ManageDtos
        public int PaidCourtCount { get; set; }
        public int UnpaidCourtCount { get; set; }
        public decimal PaidCourtAmount { get; set; }
        public decimal UnpaidCourtAmount { get; set; }
        public decimal PaidShuttleAmount { get; set; }
        public decimal UnpaidShuttleAmount { get; set; }
        public decimal TotalAdditions { get; set; }
        public decimal TotalSubtractions { get; set; }

        // --- NEW: สรุปยอดสุทธิสำหรับผู้จัด (Net Income) ---
        public decimal TotalServiceFeeDeducted { get; set; }
        public decimal OrganizerNetTotalIncome { get; set; }
        public decimal OrganizerNetPaidAmount { get; set; }
        public decimal OrganizerNetUnpaidAmount { get; set; }

        public List<ParticipantFinancialDto> Participants { get; set; } = new();
    }

    public record AddGuestDto(
           string GuestName,
           string? PhoneNumber,
           int Gender,
           int? SkillLevelId
       );

   public record UpdateSkillLevelDto(
        int? SkillLevelId
    );

      public class AutoMatchRequestDto
    {
        public bool IsMixedMode { get; set; }
        public List<string> ExcludedPlayerIds { get; set; } = new(); // ID ของคนที่ Pause/End เช่น ["Member_1", "Guest_5"]
    }

     public class SwapPlayersRequestDto
    {
        public PlayerSelectionDto Player1 { get; set; }
        public PlayerSelectionDto Player2 { get; set; }
    }

      public class AssignReserveRequestDto
    {
        public string TargetCourtIdentifier { get; set; } // สนามที่จะเอาลง
        public bool IsQueueMode { get; set; } // true = ตามคิว, false = ตามเลขสนาม
    }

       public class MovePlayersRequestDto
    {
        public List<PlayerSelectionDto> Players { get; set; } = new();
        public string TargetCourtIdentifier { get; set; } // เป้าหมาย (เลขสนาม หรือ รหัสทีมสำรอง)
    }
}