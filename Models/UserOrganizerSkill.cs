using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class UserOrganizerSkill
{
    public int UserId { get; set; }

    public int OrganizerUserId { get; set; }

    public int SkillLevelId { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual User OrganizerUser { get; set; } = null!;

    public virtual OrganizerSkillLevel SkillLevel { get; set; } = null!;
}
