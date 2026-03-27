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
        short PhoneVisibility,
        short FacebookVisibility,
        short LineVisibility
    );

    public record FullOrganizerProfileDto(
        // From UserProfiles
        string Nickname,
        string FirstName,
        string LastName,
        string Email,
        string PhoneNumber,
        short? Gender,
        string? EmergencyContactName,
        string? EmergencyContactPhone,

    // From OrganizerProfiles
       string? ProfilePhotoUrl,
       string NationalId,
       int BankId,
       string BankAccountNumber,
       string? BankAccountPhotoUrl,
       string PublicPhoneNumber,
       string? FacebookLink,
       string? LineId,
       short PhoneVisibility,
       short FacebookVisibility,
       short LineVisibility,
       int Status
   );

    public record ProfileAndOrganizerDto(
         string FirstName,
         string LastName,
         string Email,
         short? Gender,
         string? ProfilePhotoUrl,
         string? EmergencyContactName,
         string? EmergencyContactPhone,

        string PublicPhoneNumber,
        string? FacebookLink,
        string? LineId,
        short PhoneVisibility,
        short FacebookVisibility,
        short LineVisibility
    );

    public record TransferBookingDto(
        string NationalId,
        int BankId,
        string BankAccountNumber,
        string? BankAccountPhotoUrl
    );
}