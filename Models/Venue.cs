using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class Venue
{
    public int VenueId { get; set; }

    public string GooglePlaceId { get; set; } = null!;

    public string VenueName { get; set; } = null!;

    public string? Address { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public DateTime? FirstUsedDate { get; set; }

    public int? FirstUsedByUserId { get; set; }

    public virtual ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}
