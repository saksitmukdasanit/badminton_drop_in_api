using DropInBadAPI.Models;
using System.Security.Claims;

namespace DropInBadAPI.Interfaces
{
    public interface IJwtService
    {
         string CreateAccessToken(User user);
        string CreateRefreshToken();
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}