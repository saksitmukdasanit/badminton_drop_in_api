using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class SessionParticipant
{
    public int ParticipantId { get; set; }

    public int UserId { get; set; }

    public int SessionId { get; set; }

    public int? SkillLevelId { get; set; }

    public DateTime? CheckinTime { get; set; }

    public DateTime? CheckoutTime { get; set; }

    public byte? Status { get; set; }

    public DateTime JoinedDate { get; set; }

    public virtual GameSession Session { get; set; } = null!;

    public virtual OrganizerSkillLevel? SkillLevel { get; set; }

    public virtual User User { get; set; } = null!;
}
