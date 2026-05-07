namespace DropInBadAPI.Dtos
{
    public record GoogleLoginDto(string IdToken);

    public record AppleLoginDto(
        string IdentityToken,
        string? AuthorizationCode,
        string? FullName,
        string? Email
    );

    public record SocialLoginResponseDto(
        string AccessToken,
        string RefreshToken,
        bool RequiresPhoneVerification,
        string? PhoneNumber
    );

    public record VerifiedSocialIdentity(
        string ProviderName,
        string ProviderKey,
        string? Email,
        string? Name
    );
}
