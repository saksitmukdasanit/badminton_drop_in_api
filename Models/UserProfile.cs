using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class UserProfile
{
    public int UserId { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? PrimaryContactEmail { get; set; }

    public string? Nickname { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public byte? Gender { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsPhoneNumberVerified { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? Otpcode { get; set; }

    public DateTime? OtpexpiryDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual User User { get; set; } = null!;
}
