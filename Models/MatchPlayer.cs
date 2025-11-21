using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class MatchPlayer
{
    public int MatchPlayerId { get; set; }

    public int MatchId { get; set; }

    public int? UserId { get; set; }

    public int? WalkinId { get; set; }

    public string Team { get; set; } = null!;

    public short? Result { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual User? User { get; set; }

    public virtual SessionWalkinGuest? Walkin { get; set; }
}
