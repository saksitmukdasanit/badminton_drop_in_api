using System;

namespace DropInBadAPI.Models;

public partial class OrganizerRecurringGameTemplate
{
    public int RecurringTemplateId { get; set; }

    public int CreatedByUserId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>บิต 0 = จันทร์ … บิต 6 = อาทิตย์ (สอดคล้อง mask จาก Flutter: weekday 1→บิต 0)</summary>
    public short DaysOfWeekMask { get; set; }

    public string GroupName { get; set; } = null!;

    public string GooglePlaceId { get; set; } = null!;

    public string VenueNameSnapshot { get; set; } = null!;

    public string AddressSnapshot { get; set; } = null!;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int? GameTypeId { get; set; }

    public int? PairingMethodId { get; set; }

    public int MaxParticipants { get; set; }

    public short? CostingMethod { get; set; }

    public decimal? CourtFeePerPerson { get; set; }

    public decimal? ShuttlecockFeePerPerson { get; set; }

    public decimal? TotalCourtCost { get; set; }

    public decimal? ShuttlecockCostPerUnit { get; set; }

    public int? ShuttlecockModelId { get; set; }

    public int? NumberOfCourts { get; set; }

    public string? CourtNumbers { get; set; }

    public string? Notes { get; set; }

    public string? FacilityIdsCsv { get; set; }

    public string? PhotoUrlsJson { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;
}
