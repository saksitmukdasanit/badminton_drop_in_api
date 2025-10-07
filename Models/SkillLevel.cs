using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class SkillLevel
{
    public int SkillLevelId { get; set; }

    public string LevelName { get; set; } = null!;

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual ICollection<SessionWalkinGuest> SessionWalkinGuests { get; set; } = new List<SessionWalkinGuest>();
}
