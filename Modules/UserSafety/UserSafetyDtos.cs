namespace DropInBadAPI.Modules.UserSafety;

public record ReportUserDto(int ReportedUserId, string Reason, string? Description, int? SessionId);

public record BlockUserDto(int BlockedUserId);

public record BlockedUserItemDto(int UserId, string? Nickname, string? ProfilePhotoUrl, DateTime CreatedAt);
