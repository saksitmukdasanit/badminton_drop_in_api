using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class Facility
{
    public int FacilityId { get; set; }

    public string FacilityName { get; set; } = null!;

    public string? IconUrl { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual ICollection<GameSessionFacility> GameSessionFacilities { get; set; } = new List<GameSessionFacility>();
}
