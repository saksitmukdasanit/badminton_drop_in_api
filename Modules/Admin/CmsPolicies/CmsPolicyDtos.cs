namespace DropInBadAPI.Modules.Admin;

public record CmsPolicyDocumentDto(
    int CmsPolicyDocumentId,
    string Title,
    string Slug,
    string Body,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record CmsPolicyDocumentCreateDto(string Title, string Slug, string Body, int SortOrder, bool IsActive);

public record CmsPolicyDocumentUpdateDto(string Title, string Slug, string Body, int SortOrder, bool IsActive);

