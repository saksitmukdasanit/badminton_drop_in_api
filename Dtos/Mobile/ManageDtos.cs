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
        public int? Gender { get; set; }
        public string? ProfilePhotoUrl { get; set; }

        // ข้อมูลระดับมือ
        public int? SkillLevelId { get; set; }
        public string? SkillLevelName { get; set; }
        public string? SkillLevelColor { get; set; }

        public int Status { get; set; } // สถานะการเข้าร่วม: 1=เข้าร่วม, 2=รอคิว
        public DateTime? CheckinTime { get; set; }
    }
    
       public class LiveSessionStateDto
    {
        public List<CurrentlyPlayingMatchDto> CurrentlyPlaying { get; set; } = new();
        public List<WaitingPlayerDto> WaitingPool { get; set; } = new(); // ผู้เล่นทั้งหมดที่เช็คอินแล้วและกำลังรอเล่น
    }

    public class CurrentlyPlayingMatchDto
    {
        public int MatchId { get; set; }
        public int CourtNumber { get; set; }
        public DateTime StartTime { get; set; }
        public List<PlayerInMatchDto> TeamA { get; set; } = new();
        public List<PlayerInMatchDto> TeamB { get; set; } = new();
    }

    public class PlayerInMatchDto 
    {
        public int? UserId { get; set; }
        public int? WalkinId { get; set; }
        public string? Nickname { get; set; }
    }

    public class WaitingPlayerDto
    {
        public int ParticipantId { get; set; } // เป็น ParticipantID หรือ WalkinID
        public required string ParticipantType { get; set; } // "Member" หรือ "Guest"
        public string? Nickname { get; set; }
        public string? ProfilePhotoUrl { get; set; }
        public string? SkillLevelName { get; set; }
        public string? SkillLevelColor { get; set; }
        public DateTime CheckedInTime { get; set; }
    }

    // ====== DTOs for POST /matches ======
    
    public class CreateMatchDto
    {
        public int CourtNumber { get; set; }
        public List<ParticipantIdentifierDto> TeamA { get; set; } = new();
        public List<ParticipantIdentifierDto> TeamB { get; set; } = new();
    }

    public class ParticipantIdentifierDto
    {
        public int Id { get; set; } // ParticipantID หรือ WalkinID
        public required string Type { get; set; } // "Member" หรือ "Guest"
    }

    // ====== DTOs for PUT /my-result ======

    public record SubmitResultDto(int Result, string? Notes); // Result: 1=Win, 2=Loss, 3=Draw

    // ====== DTOs for POST /checkout ======
    
    public class BillSummaryDto
    {
        public int BillId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<BillLineItemDto> LineItems { get; set; } = new();
    }
    public class BillLineItemDto
    {
        public required string Description { get; set; }
        public decimal Amount { get; set; }
    }
}