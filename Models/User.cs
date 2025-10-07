using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class User
{
    public int UserId { get; set; }

    public Guid UserPublicId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();

    public virtual ICollection<MatchPlayer> MatchPlayers { get; set; } = new List<MatchPlayer>();

    public virtual OrganizerProfile? OrganizerProfile { get; set; }

    public virtual ICollection<OrganizerSkillLevel> OrganizerSkillLevels { get; set; } = new List<OrganizerSkillLevel>();

    public virtual ICollection<ParticipantBill> ParticipantBills { get; set; } = new List<ParticipantBill>();

    public virtual ICollection<SessionParticipant> SessionParticipants { get; set; } = new List<SessionParticipant>();

    public virtual ICollection<UserLogin> UserLogins { get; set; } = new List<UserLogin>();

    public virtual UserProfile? UserProfile { get; set; }
}
