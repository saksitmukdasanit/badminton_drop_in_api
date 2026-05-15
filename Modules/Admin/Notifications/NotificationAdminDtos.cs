namespace DropInBadAPI.Modules.Admin;

public record NotificationAdminListItemDto(
    int NotificationId,
    int UserId,
    string UserNicknameOrEmail,
    string Title,
    string Message,
    string Type,
    int? ReferenceId,
    bool IsRead,
    DateTime CreatedDate);

public record NotificationAdminCreateDto(int UserId, string Title, string Message, string Type, int? ReferenceId);

