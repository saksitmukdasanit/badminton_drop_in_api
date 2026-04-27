using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class UserWallet
{
    public int WalletId { get; set; }
    public int UserId { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public virtual User User { get; set; } = null!;
    
    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}