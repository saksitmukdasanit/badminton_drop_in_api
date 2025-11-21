using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class OrganizerProfile
{
    public int UserId { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? NationalId { get; set; }

    public int BankId { get; set; }

    public string BankAccountNumber { get; set; } = null!;

    public string? BankAccountPhotoUrl { get; set; }

    public string? PublicPhoneNumber { get; set; }

    public string? FacebookLink { get; set; }

    public string? LineId { get; set; }

    public short PhoneVisibility { get; set; }

    public short FacebookVisibility { get; set; }

    public short LineVisibility { get; set; }

    public short Status { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual Bank Bank { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
