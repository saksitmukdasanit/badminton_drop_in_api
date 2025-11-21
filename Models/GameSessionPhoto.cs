using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class GameSessionPhoto
{
    public int PhotoId { get; set; }

    public int SessionId { get; set; }

    public string PhotoUrl { get; set; } = null!;

    public short? DisplayOrder { get; set; }

    public string? Caption { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public virtual GameSession Session { get; set; } = null!;
}
