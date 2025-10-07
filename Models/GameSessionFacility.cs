using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class GameSessionFacility
{
    public int SessionId { get; set; }

    public int FacilityId { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public virtual Facility Facility { get; set; } = null!;

    public virtual GameSession Session { get; set; } = null!;
}
