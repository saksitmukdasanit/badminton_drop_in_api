namespace DropInBadAPI.Dtos
{
    // DTO สำหรับรับข้อมูลตอนสร้างและแก้ไขก๊วน
    public record SaveGameSessionDto(
        string GroupName,
        int VenueId,
        DateTime SessionDate,
        TimeSpan Duration,
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
        public List<FacilityDto> Facilities { get; set; } = new(); // สมมติว่ามี FacilityDto
        // ... เพิ่ม Properties อื่นๆ ที่ต้องการแสดงผล ...
    }

    public record FacilityDto(int FacilityId, string FacilityName);
}