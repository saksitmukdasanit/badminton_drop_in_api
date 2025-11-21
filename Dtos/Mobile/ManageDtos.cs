namespace DropInBadAPI.Dtos
{
    // DTO หลักสำหรับหน้า Manage
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

        // ข้อมูลอื่นๆ
        public string? Notes { get; set; }
        public List<string> PhotoUrls { get; set; } = new();

        // รายชื่อผู้เข้าร่วมทั้งหมด (ทั้งสมาชิกและ Walk-in)
        public List<ParticipantDto> Participants { get; set; } = new();
    }

    // DTO สำหรับผู้เข้าร่วมแต่ละคน
    public class ParticipantDto
    {
        public int ParticipantId { get; set; } // ID จากตาราง SessionParticipants หรือ WalkinID
        public required string ParticipantType { get; set; } // "Member" หรือ "Guest"
        public int? UserId { get; set; } // มีค่าถ้าเป็น Member
        public string? Nickname { get; set; }
        public string? FullName { get; set; }
        public string? GenderName { get; set; }
        public string? ProfilePhotoUrl { get; set; }

        // ข้อมูลระดับมือ
        public int? SkillLevelId { get; set; }
        public string? SkillLevelName { get; set; }
        public string? SkillLevelColor { get; set; }

        public int Status { get; set; } // สถานะการเข้าร่วม: 1=เข้าร่วม, 2=รอคิว
        public DateTime? CheckinTime { get; set; }
    }

    public record AddGuestDto(
        string GuestName,
        int Gender,
        int? SkillLevelId
    );

    public record UpdateSkillLevelDto(
        int? SkillLevelId
    );

    public class LiveSessionStateDto
    {
        public string groupName { get; set; }
        public List<CourtStatusDto> Courts { get; set; } = new();
        public List<WaitingPlayerDto> WaitingPool { get; set; } = new();
        public List<StagedMatchDto> StagedMatches { get; set; } = new(); // เพิ่มส่วนสำหรับแสดงแมตช์ที่จัดล่วงหน้า
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
    public record SubmitResultDto(int Result, string? Notes); // 1=Win, 2=Loss, 3=Draw

    // ====== DTOs for POST /checkout ======
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

    // ====== DTOs for NEW APIs ======

    // DTO สำหรับ Scan QR Code (รับ UserId/PublicId)
    public class CheckinDto
    {
        public string? ScannedData { get; set; }

        public int? ParticipantId { get; set; }
        public string? ParticipantType { get; set; } // "Member" or "Guest"
    }

    // DTO สำหรับเพิ่ม Walk-in
    public record AddWalkinDto(string GuestName, int? Gender, int? SkillLevelId);

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
    }

}