using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class UserLogin
{
    public string ProviderName { get; set; } = null!;

    public string ProviderKey { get; set; } = null!;

    public int UserId { get; set; }

    public string? PasswordHash { get; set; }

    public string? ProviderEmail { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiryTime { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual User User { get; set; } = null!;
}
