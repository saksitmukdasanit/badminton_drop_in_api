using DropInBadAPI.Dtos;
using Google.Apis.Auth;

namespace DropInBadAPI.Modules.Auth;

public class GoogleTokenVerifier : IGoogleTokenVerifier
{
    public const string ProviderName = "Google";

    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleTokenVerifier> _logger;

    public GoogleTokenVerifier(IConfiguration configuration, ILogger<GoogleTokenVerifier> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<VerifiedSocialIdentity?> VerifyAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        // ถ้าผู้จัดยังไม่ได้ตั้งค่า Client IDs เลย → ไม่ verify (กัน prod ลืมตั้งค่าแล้ว spoof)
        var allowedAudiences = GetAllowedAudiences();
        if (allowedAudiences.Count == 0)
        {
            _logger.LogWarning("Google sign-in rejected: no Auth:Google client IDs configured.");
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = allowedAudiences
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            var picture = string.IsNullOrWhiteSpace(payload.Picture) ? null : payload.Picture.Trim();
            return new VerifiedSocialIdentity(
                ProviderName: ProviderName,
                ProviderKey: payload.Subject,
                Email: payload.Email,
                Name: payload.Name,
                ProfilePhotoUrl: picture
            );
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Google ID token validation failed.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error verifying Google ID token.");
            return null;
        }
    }

    private List<string> GetAllowedAudiences()
    {
        var ids = new List<string>();
        var keys = new[]
        {
            "Auth:Google:IosClientId",
            "Auth:Google:AndroidClientId",
            "Auth:Google:WebClientId"
        };
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                ids.Add(value);
            }
        }
        return ids;
    }
}
