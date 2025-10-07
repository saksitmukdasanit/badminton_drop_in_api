using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class Bank
{
    public int BankId { get; set; }

    public string BankName { get; set; } = null!;

    public string? BankCode { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual ICollection<OrganizerProfile> OrganizerProfiles { get; set; } = new List<OrganizerProfile>();
}
