using DropInBadAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Modules.Public;

[ApiController]
[Route("api/public/app-content")]
[AllowAnonymous]
public class CmsPublicController : ControllerBase
{
    private readonly ICmsPublicService _svc;

    public CmsPublicController(ICmsPublicService svc)
    {
        _svc = svc;
    }

    [HttpGet("profile-about")]
    public async Task<ActionResult<Response<PublicAppProfileAboutDto>>> GetProfileAbout()
    {
        var data = await _svc.GetAppProfileAboutAsync();
        return Ok(new Response<PublicAppProfileAboutDto>
        {
            Status = 200,
            Message = "OK",
            Data = data
        });
    }
}
