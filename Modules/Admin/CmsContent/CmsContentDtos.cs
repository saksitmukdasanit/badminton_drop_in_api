namespace DropInBadAPI.Modules.Admin;

public record CmsContentItemDto(
    int CmsContentItemId,
    short ContentType,
    string? Title,
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    bool IsActive,
    DateTime? ValidFromUtc,
    DateTime? ValidToUtc,
    short Platform,
    string? ExtraJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record CmsContentItemCreateDto(
    short ContentType,
    string? Title,
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    bool IsActive,
    DateTime? ValidFromUtc,
    DateTime? ValidToUtc,
    short Platform,
    string? ExtraJson
);

public record CmsContentItemUpdateDto(
    string? Title,
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    bool IsActive,
    DateTime? ValidFromUtc,
    DateTime? ValidToUtc,
    short Platform,
    string? ExtraJson
);

