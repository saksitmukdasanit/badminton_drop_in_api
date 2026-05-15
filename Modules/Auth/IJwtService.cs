using DropInBadAPI.Models;
using System.Security.Claims;

namespace DropInBadAPI.Interfaces
{
    public interface IJwtService
    {
         string CreateAccessToken(User user);
        /// <summary>Access token สำหรับ CMS admin — สั้นกว่าและมี role Admin</summary>
        string CreateAdminAccessToken(CmsAdminUser admin);
        string CreateRefreshToken();
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}