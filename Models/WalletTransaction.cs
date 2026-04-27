using System;

namespace DropInBadAPI.Models;

public partial class WalletTransaction
{
    public int TransactionId { get; set; }
    public int WalletId { get; set; }
    public decimal Amount { get; set; }
    public short TransactionType { get; set; } // 1 = IN (Refund), 2 = OUT (Payment)
    public string? Description { get; set; }
    public int? ReferenceId { get; set; }
    public DateTime CreatedDate { get; set; }

    public virtual UserWallet Wallet { get; set; } = null!;
}