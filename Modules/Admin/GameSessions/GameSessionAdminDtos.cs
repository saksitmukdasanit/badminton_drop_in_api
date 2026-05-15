namespace DropInBadAPI.Modules.Admin;

public record GameSessionAdminListItemDto(
    int SessionId,
    Guid SessionPublicId,
    string GroupName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    short? Status,
    string VenueName,
    int CreatedByUserId,
    string? OrganizerNickname,
    DateTime CreatedDate);

public record GameSessionAdminDetailDto(
    int SessionId,
    Guid SessionPublicId,
    string GroupName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    short? Status,
    string? Notes,
    int VenueId,
    string VenueName,
    int CreatedByUserId,
    string? OrganizerNickname,
    int MaxParticipants,
    DateTime CreatedDate,
    DateTime? UpdatedDate);

public record GameSessionAdminUpdateDto(
    short? Status,
    string? Notes,
    string? GroupName);

