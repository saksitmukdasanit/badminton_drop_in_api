namespace DropInBadAPI.Modules.Admin;

public record CmsAboutSettingsDto(string Title, string Body, DateTime UpdatedAtUtc);

public record CmsAboutSettingsUpdateDto(string Title, string Body);

