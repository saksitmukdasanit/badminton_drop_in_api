using DropInBadAPI.Models;
using DropInBadAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using DropInBadAPI.Dtos;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DropInBadAPI.Controllers.Mobile
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class FollowsController : ControllerBase
    {
        private readonly IFollowService _followService;

        public FollowsController(IFollowService followService)
        {
            _followService = followService;
        }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("{organizerId}/follow")]
        public async Task<ActionResult<Response<object>>> ToggleFollow(int organizerId)
        {
            var (success, errorMessage) = await _followService.ToggleFollowAsync(GetCurrentUserId(), organizerId);
            if (!success) return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            return Ok(new Response<object> { Status = 200, Message = "Follow status updated." });
        }

        [HttpGet("my-followed")]
        public async Task<ActionResult<Response<IEnumerable<OrganizerSummaryDto>>>> GetMyFollowedOrganizers()
        {
            var organizers = await _followService.GetFollowedOrganizersAsync(GetCurrentUserId());
            return Ok(new Response<IEnumerable<OrganizerSummaryDto>> { Status = 200, Message = "Success", Data = organizers });
        }
    }
}