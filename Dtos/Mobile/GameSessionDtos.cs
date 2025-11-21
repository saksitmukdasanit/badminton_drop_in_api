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
        public int? NumberOfCourts { get; set; }
        public string? CourtNumbers { get; set; }
        public string? Notes { get; set; }
        public List<int> FacilityIds { get; set; }
        public List<string> PhotoUrls { get; set; }
        public List<ParticipantDto> Participants { get; set; } = new();
        public int Status { get; set; }

    }


    public record FacilityDto(int FacilityId, string FacilityName, string IconUrl);

    // DTO หลักสำหรับแสดงรายละเอียดก๊วนในมุมมองผู้เล่น
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

    // DTO ย่อยสำหรับข้อมูลผู้จัด
    public class OrganizerInfoDto
    {
        public int UserId { get; set; }
        public string Nickname { get; set; }
        public string? ProfilePhotoUrl { get; set; }
    }

    public record UpcomingSessionCardDto
    {
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
        public string? Price { get; init; }
        public string? CourtFeePerPerson { get; init; }
        public string? ShuttlecockFeePerPerson { get; init; }
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

        public List<FacilityDto> Facilities { get; set; } = new();
        public List<ParticipantDto> Participants { get; set; } = new();
    }

    public record JoinSessionResponseDto
    {
        public int ParticipantId { get; init; }
        public int Status { get; init; } // 1=เข้าร่วม, 2=รอคิว
        public string StatusMessage { get; init; } = string.Empty; // "เข้าร่วมสำเร็จ" หรือ "คุณอยู่ในคิวสำรอง"
    }
}