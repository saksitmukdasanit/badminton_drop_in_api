using DropInBadAPI.Dtos;

namespace DropInBadAPI.Modules.Auth;

public interface IAppleTokenVerifier
{
    Task<VerifiedSocialIdentity?> VerifyAsync(string identityToken);
}
