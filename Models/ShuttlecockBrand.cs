using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class ShuttlecockBrand
{
    public int BrandId { get; set; }

    public string BrandName { get; set; } = null!;

    public bool? IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public virtual ICollection<ShuttlecockModel> ShuttlecockModels { get; set; } = new List<ShuttlecockModel>();
}
