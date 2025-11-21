using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int BillId { get; set; }

    public short PaymentMethod { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public int? ReceivedByUserId { get; set; }

    public virtual ParticipantBill Bill { get; set; } = null!;
}
