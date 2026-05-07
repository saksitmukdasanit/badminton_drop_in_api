using DropInBadAPI.Dtos;

namespace DropInBadAPI.Modules.Auth;

public interface IGoogleTokenVerifier
{
    Task<VerifiedSocialIdentity?> VerifyAsync(string idToken);
}
