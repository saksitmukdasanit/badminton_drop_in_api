using System;

namespace DropInBadAPI.Models;

public partial class UserFcmToken
{
    public int TokenId { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = null!;
    public string? DeviceName { get; set; } // เผื่อไว้เก็บชื่อเครื่องในอนาคต (Nullable)
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;
}