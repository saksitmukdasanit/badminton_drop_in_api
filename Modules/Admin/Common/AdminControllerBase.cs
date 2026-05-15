using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

public abstract class AdminControllerBase : ControllerBase
{
    protected int? GetCmsAdminId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
