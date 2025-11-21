using System;
using System.Collections.Generic;

namespace DropInBadAPI.Models;

public partial class GameSession
{
    public int SessionId { get; set; }

    public Guid SessionPublicId { get; set; }

    public string GroupName { get; set; } = null!;

    public int VenueId { get; set; }

    public DateOnly SessionDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int? GameTypeId { get; set; }

    public int? PairingMethodId { get; set; }

    public int MaxParticipants { get; set; }

    public short? CostingMethod { get; set; }

    public decimal? CourtFeePerPerson { get; set; }

    public decimal? ShuttlecockFeePerPerson { get; set; }

    public decimal? TotalCourtCost { get; set; }

    public decimal? ShuttlecockCostPerUnit { get; set; }

    public int? ShuttlecockModelId { get; set; }

    public int? NumberOfCourts { get; set; }

    public string? CourtNumbers { get; set; }

    public string? Notes { get; set; }

    public short? Status { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<GameSessionFacility> GameSessionFacilities { get; set; } = new List<GameSessionFacility>();

    public virtual ICollection<GameSessionPhoto> GameSessionPhotos { get; set; } = new List<GameSessionPhoto>();

    public virtual GameType? GameType { get; set; }

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();

    public virtual PairingMethod? PairingMethod { get; set; }

    public virtual ICollection<ParticipantBill> ParticipantBills { get; set; } = new List<ParticipantBill>();

    public virtual ICollection<SessionParticipant> SessionParticipants { get; set; } = new List<SessionParticipant>();

    public virtual ICollection<SessionWalkinGuest> SessionWalkinGuests { get; set; } = new List<SessionWalkinGuest>();

    public virtual ShuttlecockModel? ShuttlecockModel { get; set; }

    public virtual Venue Venue { get; set; } = null!;
}
