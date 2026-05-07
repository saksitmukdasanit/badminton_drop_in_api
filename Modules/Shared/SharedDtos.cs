namespace DropInBadAPI.Dtos
{
    public record UpcomingSessionCardDto
    {
        public Guid SessionPublicId { get; init; }
        public int SessionId { get; init; }
        public string GroupName { get; init; } = string.Empty; // << เพิ่มกลับเข้ามา
        public string? ImageUrl { get; init; }
        public string DayOfWeek { get; init; } = string.Empty;
        public string SessionDate { get; init; } = string.Empty;
        public string StartTime { get; init; } = string.Empty;
        public string EndTime { get; init; } = string.Empty;
        public DateTime SessionStart { get; init; } // << เพิ่มกลับเข้ามา (DateTime เต็ม)
        public string CourtName { get; init; } = string.Empty; // เปลี่ยนจาก VenueName เป็น CourtName ตามที่คุณขอ
        public string? Location { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? Price { get; init; }
        public string? CourtFeePerPerson { get; init; }
        public string? ShuttlecockFeePerPerson { get; init; }
        public int? CostingMethod { get; init; }
        public string OrganizerName { get; init; } = string.Empty;
        public string? OrganizerImageUrl { get; init; }
        public bool IsBookmarked { get; init; }
        public int CurrentParticipants { get; init; }
        public int MaxParticipants { get; init; }

        public string? GameTypeName { get; init; }
        public string? ShuttlecockBrandName { get; init; }
        public string? ShuttlecockModelName { get; init; }
        public short? Status { get; init; }
        public List<string>? CourtImageUrls { get; init; }
        public string? CourtNumbers { get; init; }
        public string? Notes { get; init; }
        public bool CanStartSession { get; set; }

        public List<FacilityDto> Facilities { get; set; } = new();
        public List<ParticipantDto> Participants { get; set; } = new();
        public decimal PaidAmount { get; set; }
        public decimal TotalIncome { get; set; }
        public string? UserStatus { get; set; } // สถานะของผู้เล่นในก๊วน: Joined, Waitlisted, Refund
    }

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
        public DateTime? CheckoutTime { get; set; }
        public int TotalGamesPlayed { get; set; } // NEW: เพิ่มฟิลด์จำนวนเกมที่เล่น
    }

    public class MyGameSessionsResponseDto
    {
        public List<UpcomingSessionCardDto> Playing { get; set; } = new();
        public List<UpcomingSessionCardDto> Upcoming { get; set; } = new();
        public List<UpcomingSessionCardDto> Refund { get; set; } = new();
    }
}