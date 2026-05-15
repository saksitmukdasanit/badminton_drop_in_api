using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DropInBadAPI.Dtos;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace DropInBadAPI.Modules.Auth;

/// <summary>
/// ตรวจสอบ identity token ของ Sign in with Apple (JWT)
/// - ดึง public keys จาก https://appleid.apple.com/.well-known/openid-configuration (cache อัตโนมัติ 24 ชม.)
/// - ตรวจ issuer = https://appleid.apple.com
/// - ตรวจ audience = Bundle ID / Service ID ของแอป (รองรับหลายค่า เช่น iOS bundle + web service)
/// </summary>
public class AppleTokenVerifier : IAppleTokenVerifier
{
    public const string ProviderName = "Apple";
    private const string AppleIssuer = "https://appleid.apple.com";
    private const string AppleOpenIdConfig = "https://appleid.apple.com/.well-known/openid-configuration";

    private readonly IConfiguration _configuration;
    private readonly ILogger<AppleTokenVerifier> _logger;
    private readonly Lazy<ConfigurationManager<OpenIdConnectConfiguration>> _configManager;

    public AppleTokenVerifier(IConfiguration configuration, ILogger<AppleTokenVerifier> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _configManager = new Lazy<ConfigurationManager<OpenIdConnectConfiguration>>(
            () => new ConfigurationManager<OpenIdConnectConfiguration>(
                AppleOpenIdConfig,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true })
        );
    }

    public async Task<VerifiedSocialIdentity?> VerifyAsync(string identityToken)
    {
        if (string.IsNullOrWhiteSpace(identityToken))
        {
            return null;
        }

        var allowedAudiences = GetAllowedAudiences();
        if (allowedAudiences.Count == 0)
        {
            _logger.LogWarning("Apple sign-in rejected: no Auth:Apple audiences configured.");
            return null;
        }

        try
        {
            var openIdConfig = await _configManager.Value.GetConfigurationAsync(CancellationToken.None);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = AppleIssuer,
                ValidateAudience = true,
                ValidAudiences = allowedAudiences,
                ValidateLifetime = true,
                IssuerSigningKeys = openIdConfig.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(identityToken, validationParameters, out _);

            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrEmpty(sub))
            {
                _logger.LogWarning("Apple identity token missing 'sub' claim.");
                return null;
            }

            var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
            return new VerifiedSocialIdentity(
                ProviderName: ProviderName,
                ProviderKey: sub,
                Email: email,
                Name: null, // Apple ส่ง full name มาเฉพาะตอนสมัครครั้งแรกผ่าน authorization code, ไม่อยู่ใน identity token
                ProfilePhotoUrl: null // Sign in with Apple ไม่ส่งรูปใน identity token
            );
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Apple identity token validation failed.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error verifying Apple identity token.");
            return null;
        }
    }

    private List<string> GetAllowedAudiences()
    {
        var audiences = new List<string>();

        var bundleId = _configuration["Auth:Apple:BundleId"];
        if (!string.IsNullOrWhiteSpace(bundleId)) audiences.Add(bundleId);

        var serviceId = _configuration["Auth:Apple:ServiceId"];
        if (!string.IsNullOrWhiteSpace(serviceId)) audiences.Add(serviceId);

        // รองรับ comma-separated เผื่อมีหลายแอป (เช่น dev + prod bundle ต่างกัน)
        var extraAudiences = _configuration["Auth:Apple:AdditionalAudiences"];
        if (!string.IsNullOrWhiteSpace(extraAudiences))
        {
            audiences.AddRange(extraAudiences.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return audiences;
    }
}
