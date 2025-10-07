using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class ShuttlecockModel
{
    public int ModelId { get; set; }

    public string ModelName { get; set; } = null!;

    public int BrandId { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual ShuttlecockBrand Brand { get; set; } = null!;

    public virtual ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}
