using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class Match
{
    public int MatchId { get; set; }

    public int SessionId { get; set; }

    public int CourtNumber { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public byte? Status { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public virtual ICollection<MatchPlayer> MatchPlayers { get; set; } = new List<MatchPlayer>();

    public virtual GameSession Session { get; set; } = null!;
}
