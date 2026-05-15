namespace DropInBadAPI.Modules.Admin;

public record OrganizerListItemDto(
    int UserId,
    short Status,
    string? OrganizerPhone,
    string? OrganizerNickname,
    int BankId,
    string BankAccountNumber,
    DateTime CreatedDate);

public record OrganizerDetailDto(
    int UserId,
    short Status,
    string? ProfilePhotoUrl,
    string? NationalId,
    int BankId,
    string BankAccountNumber,
    string? BankAccountPhotoUrl,
    string? PublicPhoneNumber,
    string? FacebookLink,
    string? LineId,
    short PhoneVisibility,
    short FacebookVisibility,
    short LineVisibility,
    string? XenditAccountId,
    string? UserNickname,
    string? UserPhone,
    string? UserEmail,
    AdminWalletSummaryDto Wallet);

public record OrganizerCreateDto(
    int UserId,
    int BankId,
    string BankAccountNumber,
    string? BankAccountPhotoUrl,
    string? PublicPhoneNumber,
    string? ProfilePhotoUrl,
    short Status);

public record OrganizerUpdateDto(
    int BankId,
    string BankAccountNumber,
    string? BankAccountPhotoUrl,
    string? PublicPhoneNumber,
    string? ProfilePhotoUrl,
    string? FacebookLink,
    string? LineId,
    short PhoneVisibility,
    short FacebookVisibility,
    short LineVisibility,
    short Status,
    string? NationalId,
    string? XenditAccountId);

