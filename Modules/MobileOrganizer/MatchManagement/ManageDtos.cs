#nullable disable warnings
namespace DropInBadAPI.Dtos
{

    public class LiveSessionStateDto
    {
        public string groupName { get; set; }
        public List<CourtStatusDto> Courts { get; set; } = new();
        public List<WaitingPlayerDto> WaitingPool { get; set; } = new();
        public List<StagedMatchDto> StagedMatches { get; set; } = new(); // เพิ่มส่วนสำหรับแสดงแมตช์ที่จัดล่วงหน้า
        public DateTime? CompetitionStartTime { get; set; }
    }

    public class CourtStatusDto
    {
        public string CourtIdentifier { get; set; } // ชื่อหรือหมายเลขสนาม เช่น "1", "2", "A1"
        public CurrentlyPlayingMatchDto? CurrentMatch { get; set; } // ข้อมูลแมตช์ที่กำลังเล่นอยู่ หรือเป็น null ถ้าสนามว่าง
    }

    public class CurrentlyPlayingMatchDto
    {
        public int MatchId { get; set; }
        public string? CourtNumber { get; set; }
        public DateTime? StartTime { get; set; }
        public List<PlayerInMatchDto> TeamA { get; set; } = new();
        public List<PlayerInMatchDto> TeamB { get; set; } = new();
    }

    public class PlayerInMatchDto
    {
        public int? UserId { get; set; }
        public int? WalkinId { get; set; }
        public string Nickname { get; set; }
        public string? ProfilePhotoUrl { get; set; }
        public string? GenderName { get; set; }
        public int? SkillLevelId { get; set; }
        public string? SkillLevelName { get; set; }
        public string? SkillLevelColor { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
    }

    public class WaitingPlayerDto
    {
        public int ParticipantId { get; set; } // ParticipantID หรือ WalkinID
        public string ParticipantType { get; set; } // "Member" หรือ "Guest"
        public string Nickname { get; set; }
        public string? ProfilePhotoUrl { get; set; }
        public string? GenderName { get; set; }
        public int? SkillLevelId { get; set; }
        public string? SkillLevelName { get; set; }
        public string? SkillLevelColor { get; set; }
        public DateTime CheckedInTime { get; set; }
        public int TotalGamesPlayed { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
    }

    // ====== DTOs for POST /matches ======
    // (DTOs เหล่านี้คือข้อมูลที่ Frontend ส่งมา)

    public class CreateMatchDto
    {
        public string? CourtNumber { get; set; }
        public List<PlayerSelectionDto> TeamA { get; set; } = new();
        public List<PlayerSelectionDto> TeamB { get; set; } = new();
    }

    // DTO สำหรับสร้าง Staged Match (ไม่มี CourtNumber)
    public class CreateStagedMatchDto
    {
        public string? courtIdentifier { get; set; } // เพิ่ม: สามารถระบุสนามล่วงหน้าได้
        public List<PlayerSelectionDto> TeamA { get; set; } = new();
        public List<PlayerSelectionDto> TeamB { get; set; } = new();
    }

    // DTO สำหรับแสดงผล Staged Match
    public class StagedMatchDto
    {
        public int MatchId { get; set; }
        public string? CourtNumber { get; set; } // เพิ่ม: หมายเลขสนามที่ผูกกับแมตช์นี้
        public List<PlayerInMatchDto> TeamA { get; set; } = new();
        public List<PlayerInMatchDto> TeamB { get; set; } = new();
    }

    public class PlayerSelectionDto
    {
        public int Id { get; set; } // ParticipantID หรือ WalkinID
        public string Type { get; set; } // "Member" หรือ "Guest"
    }

    // ====== DTOs for PUT /my-result ======


    // ====== DTOs for POST /checkout ======

    public class CheckoutRequestDto
    {
        public List<BillLineItemDto>? CustomLineItems { get; set; }
    }
    public class BillSummaryDto
    {
        public int BillId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<BillLineItemDto> LineItems { get; set; } = new();
    }
    public class BillLineItemDto
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
    }

    // --- NEW: DTO สำหรับการชำระเงิน ---
    public class PaymentRequestDto
    {
        public string PaymentMethod { get; set; } // "Cash", "QR Code"
        public decimal Amount { get; set; }
    }

    // --- NEW: DTO สำหรับหน้า Finance ผู้จัด ---
    public class OrganizerFinanceDashboardDto
    {
        public decimal Balance { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal ChartTotalIncome { get; set; }
        public List<FinanceChartGameDto> LatestGames { get; set; } = new();
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountPhotoUrl { get; set; }
        public string? NationalId { get; set; }
    }

    public class FinanceChartGameDto
    {
        public string Name { get; set; }
        public int PlayersCount { get; set; }
        public int PaidCount { get; set; }
    }

    public class OrganizerWithdrawRequestDto
    {
        public decimal Amount { get; set; }
    }

    // ====== DTOs for NEW APIs ======

    // DTO สำหรับ Scan QR Code (รับ UserId/PublicId)
    public class CheckinDto
    {
        public string? ScannedData { get; set; }

        public int? ParticipantId { get; set; }
        public string? ParticipantType { get; set; } // "Member" or "Guest"
    }

    // DTO สำหรับเพิ่ม Walk-in
    public record AddWalkinDto(string GuestName, string? PhoneNumber, int? Gender, int? SkillLevelId);

    // DTO สำหรับ Autocomplete Suggestion
    public class GuestSuggestionDto
    {
        public string GuestName { get; set; }
        public string? PhoneNumber { get; set; }
        public int? Gender { get; set; }
        public int? SkillLevelId { get; set; }
    }

    // DTO สำหรับอัปเดตระดับมือ
    public record UpdateParticipantSkillDto(int SkillLevelId);

    // DTO สำหรับอัปเดตสนาม
    public class UpdateCourtsDto
    {
        public List<string> CourtIdentifiers { get; set; } = new();
    }

    // DTO สำหรับการเริ่ม Staged Match
    public record StartStagedMatchDto(string CourtNumber);

    public class RecommendedMatchDto
    {
        public List<WaitingPlayerDto> TeamA { get; set; } = new();
        public List<WaitingPlayerDto> TeamB { get; set; } = new();
        public double MatchBalanceScore { get; set; } // ค่าความสมดุล (ยิ่งน้อยยิ่งดี)
        public string RecommendationReason { get; set; } // เหตุผล เช่น "จับคู่ตามเวลารอ"
    }

    // ====== DTOs for Player Session Statistics ======

    public class PlayerSessionStatsDto
    {
        public int ParticipantId { get; set; }
        public string ParticipantType { get; set; }
        public string Nickname { get; set; }
        public int TotalGamesPlayed { get; set; }
        public string TotalMinutesPlayed { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public List<PlayerMatchHistoryDto> MatchHistory { get; set; } = new();
    }

    public class PlayerMatchHistoryDto
    {
        public int MatchId { get; set; }
        public string? CourtNumber { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public string Result { get; set; } // "Win", "Loss", "Draw"
        public PlayerInMatchDto Teammate { get; set; }
        public List<PlayerInMatchDto> Opponents { get; set; } = new();
    }




    public class SessionRosterPlayerDto
    {
        public int No { get; set; }
        public string Nickname { get; set; }
        public string FullName { get; set; }
        public string Gender { get; set; }
        public int? SkillLevelId { get; set; }
        public string? SkillLevelName { get; set; }
        public string? SkillLevelColor { get; set; }
        public bool IsCheckedIn { get; set; }
        public int ParticipantId { get; set; }
        public string ParticipantType { get; set; }
        public int Status { get; set; } // NEW: เพิ่มสถานะ (1=Joined, 2=Waitlisted)
    }

    public enum SuggestionCriteria
    {
        ByWaitTime,
        ByBalancedSkill,
        Mixed
    }

}