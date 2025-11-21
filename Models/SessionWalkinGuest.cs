using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class SessionWalkinGuest
{
    public int WalkinId { get; set; }

    public int SessionId { get; set; }

    public string GuestName { get; set; } = null!;

    public short? Gender { get; set; }

    public int? SkillLevelId { get; set; }

    public decimal? AmountPaid { get; set; }

    public DateTime? PaymentDate { get; set; }

    public short? Status { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CheckinTime { get; set; }

    public DateTime? CheckoutTime { get; set; }

    public virtual ICollection<MatchPlayer> MatchPlayers { get; set; } = new List<MatchPlayer>();

    public virtual ICollection<ParticipantBill> ParticipantBills { get; set; } = new List<ParticipantBill>();

    public virtual GameSession Session { get; set; } = null!;

    public virtual OrganizerSkillLevel? SkillLevel { get; set; }
}
