using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class PairingMethod
{
    public int PairingMethodId { get; set; }

    public string MethodName { get; set; } = null!;

    public bool? IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}
