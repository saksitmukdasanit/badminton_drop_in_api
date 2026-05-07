using DropInBadAPI.Dtos;
using DropInBadAPI.Models;
using DropInBadAPI.Service.Mobile.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Mobile;

[ApiController]
[Route("api/organizer/auto-match-preset")]
[Authorize]
public class AutoMatchPresetController : ControllerBase
{
    private readonly IAutoMatchPresetService _service;

    public AutoMatchPresetController(IAutoMatchPresetService service)
    {
        _service = service;
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<Response<AutoMatchScoringWeightsDto>>> Get()
    {
        var preset = await _service.GetAsync(GetCurrentUserId());
        return Ok(new Response<AutoMatchScoringWeightsDto>
        {
            Status = 200,
            Message = "Preset retrieved successfully.",
            Data = preset
        });
    }

    [HttpPut]
    public async Task<ActionResult<Response<AutoMatchScoringWeightsDto>>> Save([FromBody] AutoMatchScoringWeightsDto dto)
    {
        var preset = await _service.SaveAsync(GetCurrentUserId(), dto);
        return Ok(new Response<AutoMatchScoringWeightsDto>
        {
            Status = 200,
            Message = "Preset saved successfully.",
            Data = preset
        });
    }

    [HttpDelete]
    public async Task<ActionResult<Response<AutoMatchScoringWeightsDto>>> Reset()
    {
        var preset = await _service.ResetAsync(GetCurrentUserId());
        return Ok(new Response<AutoMatchScoringWeightsDto>
        {
            Status = 200,
            Message = "Preset reset to defaults.",
            Data = preset
        });
    }
}
