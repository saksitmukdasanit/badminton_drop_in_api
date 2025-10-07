using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class BillLineItem
{
    public int LineItemId { get; set; }

    public int BillId { get; set; }

    public string Description { get; set; } = null!;

    public decimal Amount { get; set; }

    public virtual ParticipantBill Bill { get; set; } = null!;
}
