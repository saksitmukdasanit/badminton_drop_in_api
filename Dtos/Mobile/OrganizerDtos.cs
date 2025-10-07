namespace DropInBadAPI.Dtos
{
    public record OrganizerProfileDto(
        string? ProfilePhotoUrl,
        string NationalId,
        int BankId,
        string BankAccountNumber,
        string? BankAccountPhotoUrl,
        string PublicPhoneNumber,
        string? FacebookLink,
        string? LineId,
        byte PhoneVisibility,
        byte FacebookVisibility,
        byte LineVisibility
    );
}