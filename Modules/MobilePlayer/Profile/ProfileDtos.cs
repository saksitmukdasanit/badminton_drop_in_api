namespace DropInBadAPI.Dtos
{
    public record UpdateProfileDto(
        string Nickname,
        string FirstName,
        string LastName,
        string PrimaryContactEmail,
        int Gender,
        string? ProfilePhotoUrl,
        string? EmergencyContactName,
        string? EmergencyContactPhone
    );

    public record UpdatePhoneNumberDto(string NewPhoneNumber);

}