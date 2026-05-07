#nullable disable warnings

namespace DropInBadAPI.Dtos;

public class SaveOrganizerRecurringTemplateDto
{
    public string GroupName { get; set; }

    public VenueDataDto VenueData { get; set; }

    public int DaysOfWeekMask { get; set; }

    /// <summary>เช่น 18:30 หรือ 18:30:00</summary>
    public string StartTime { get; set; }

    public string EndTime { get; set; }

    public int? GameTypeId { get; set; }

    public int? PairingMethodId { get; set; }

    public int MaxParticipants { get; set; }

    public int? CostingMethod { get; set; }

    public decimal? CourtFeePerPerson { get; set; }

    public decimal? ShuttlecockFeePerPerson { get; set; }

    public decimal? TotalCourtCost { get; set; }

    public decimal? ShuttlecockCostPerUnit { get; set; }

    public int? ShuttlecockModelId { get; set; }

    public int? NumberOfCourts { get; set; }

    public string CourtNumbers { get; set; }

    public string Notes { get; set; }

    public List<int> FacilityIds { get; set; } = new();

    public List<string> PhotoUrls { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

public class OrganizerRecurringTemplateListDto
{
    public int RecurringTemplateId { get; set; }

    public string GroupName { get; set; } = "";

    public string VenueName { get; set; } = "";

    public int DaysOfWeekMask { get; set; }

    public bool IsActive { get; set; }

    public string StartTime { get; set; } = "";

    public string EndTime { get; set; } = "";

    public int MaxParticipants { get; set; }

    public decimal? CourtFeePerPerson { get; set; }

    public decimal? ShuttlecockFeePerPerson { get; set; }

    public string? GameTypeName { get; set; }

    public string? ShuttlecockBrandName { get; set; }

    public string? ShuttlecockModelName { get; set; }

    /// <summary>ชื่อสิ่งอำนวยความสะดวกคั่นด้วยจุลภาค (สำหรับการ์ดในแอป)</summary>
    public string? FacilityNames { get; set; }
}

public class OrganizerRecurringTemplateDetailDto
{
    public int RecurringTemplateId { get; set; }

    public VenueDataDto VenueData { get; set; } = default!;

    public int DaysOfWeekMask { get; set; }

    public string StartTime { get; set; } = "";

    public string EndTime { get; set; } = "";

    public string GroupName { get; set; } = "";

    public int? GameTypeId { get; set; }

    public int? PairingMethodId { get; set; }

    public int MaxParticipants { get; set; }

    public int? CostingMethod { get; set; }

    public decimal? CourtFeePerPerson { get; set; }

    public decimal? ShuttlecockFeePerPerson { get; set; }

    public decimal? TotalCourtCost { get; set; }

    public decimal? ShuttlecockCostPerUnit { get; set; }

    public int? ShuttlecockModelId { get; set; }

    public int? NumberOfCourts { get; set; }

    public string? CourtNumbers { get; set; }

    public string? Notes { get; set; }

    public List<int> FacilityIds { get; set; } = new();

    public List<string> PhotoUrls { get; set; } = new();

    public bool IsActive { get; set; }
}

public class RecurringTemplateActiveDto
{
    public bool IsActive { get; set; }
}
