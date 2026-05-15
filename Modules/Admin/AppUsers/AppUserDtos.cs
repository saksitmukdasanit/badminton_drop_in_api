namespace DropInBadAPI.Modules.Admin;

public record AdminWalletTransactionDto(
    int TransactionId,
    decimal Amount,
    short TransactionType,
    string TypeLabel,
    string RecipientName,
    string? Description,
    int? ReferenceId,
    DateTime CreatedDate);

public record AdminWalletSummaryDto(
    int? WalletId,
    decimal Balance,
    decimal TotalIn,
    decimal TotalOut,
    int TransactionCount,
    int PayoutCount,
    decimal PayoutAmountLast30Days,
    List<AdminWalletTransactionDto> RecentTransactions);

public record AppUserListItemDto(
    int UserId,
    Guid UserPublicId,
    bool IsActive,
    DateTime CreatedDate,
    string? Nickname,
    string? PhoneNumber,
    string? PrimaryContactEmail,
    string? FirstName,
    string? LastName);

public record AppUserDetailDto(
    int UserId,
    Guid UserPublicId,
    bool IsActive,
    DateTime CreatedDate,
    DateTime? DeletedAt,
    string? Nickname,
    string? PhoneNumber,
    string? PrimaryContactEmail,
    string? FirstName,
    string? LastName,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    short? Gender,
    string? ProfilePhotoUrl,
    bool IsPhoneNumberVerified,
    AdminWalletSummaryDto Wallet);

public record AppUserUpdateDto(
    bool IsActive,
    string? Nickname,
    string? PhoneNumber,
    string? PrimaryContactEmail,
    string? FirstName,
    string? LastName,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    short? Gender,
    string? ProfilePhotoUrl);

public record AppUserCreateDto(string? Nickname, string? PhoneNumber, string? PrimaryContactEmail);

