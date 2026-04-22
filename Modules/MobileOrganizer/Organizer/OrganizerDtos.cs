namespace DropInBadAPI.Dtos
{
    public record OrganizerProfileDto(
        int UserId,
        string Nickname,
        string? ProfilePhotoUrl,
        string? FirstName,
        string? LastName,
        string? PrimaryContactEmail,
        int? Gender,
        string? EmergencyContactName,
        string? EmergencyContactPhone,
        string NationalId,
        int? BankId,
        string? BankName,
        string BankAccountNumber,
        string? BankAccountPhotoUrl,
        string? PublicPhoneNumber,
        byte PhoneVisibility,
        string? FacebookLink,
        byte FacebookVisibility,
        string? LineId,
        byte LineVisibility,
        byte Status
    );

    public record UpdateOrganizerContactDto(
        string Nickname,
        string FirstName,
        string LastName,
        string PrimaryContactEmail,
        int Gender,
        string? ProfilePhotoUrl,
        string? EmergencyContactName,
        string? EmergencyContactPhone,
        string? PublicPhoneNumber,
        byte PhoneVisibility,
        string? FacebookLink,
        byte FacebookVisibility,
        string? LineId,
        byte LineVisibility
    );

    public record UpdateOrganizerTransferDto(
        int BankId,
        string BankAccountNumber,
        string? BankAccountPhotoUrl
    );

    public record RegisterOrganizerDto(
        string NationalId,
        int BankId,
        string BankAccountNumber,
        string? BankAccountPhotoUrl,
        string? PublicPhoneNumber,
        string? FacebookLink,
        string? LineId
    );
}