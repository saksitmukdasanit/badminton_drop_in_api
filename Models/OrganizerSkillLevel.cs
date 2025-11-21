using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class OrganizerSkillLevel
{
    public int SkillLevelId { get; set; }

    public int OrganizerUserId { get; set; }

    public short LevelRank { get; set; }

    public string LevelName { get; set; } = null!;

    public string ColorHexCode { get; set; } = null!;

    public bool? IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual User OrganizerUser { get; set; } = null!;

    public virtual ICollection<SessionParticipant> SessionParticipants { get; set; } = new List<SessionParticipant>();

    public virtual ICollection<SessionWalkinGuest> SessionWalkinGuests { get; set; } = new List<SessionWalkinGuest>();
}
