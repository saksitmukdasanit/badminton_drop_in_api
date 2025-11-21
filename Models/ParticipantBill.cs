using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class ParticipantBill
{
    public int BillId { get; set; }

    public int SessionId { get; set; }

    public int? UserId { get; set; }

    public int? WalkinId { get; set; }

    public decimal TotalAmount { get; set; }

    public short Status { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual ICollection<BillLineItem> BillLineItems { get; set; } = new List<BillLineItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual GameSession Session { get; set; } = null!;

    public virtual User? User { get; set; }

    public virtual SessionWalkinGuest? Walkin { get; set; }
}
