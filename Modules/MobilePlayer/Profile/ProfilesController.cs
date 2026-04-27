using DropInBadAPI.Dtos;
using DropInBadAPI.Models;
using DropInBadAPI.Service.Mobile.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Mobile
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfilesController : ControllerBase
    {
        private readonly IProfileService _profileService;
        public ProfilesController(IProfileService profileService) { _profileService = profileService; }
        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET: api/profiles/me
        [HttpGet("me")]
        public async Task<ActionResult<Response<UserProfileDto>>> GetMyProfile()
        {
            var userProfile = await _profileService.GetUserProfileAsync(GetCurrentUserId());
            if (userProfile == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "User profile not found." });
            }
            return Ok(new Response<UserProfileDto> { Status = 200, Message = "Profile retrieved successfully.", Data = userProfile });
        }

        // PUT: api/profiles/me
        [HttpPut("me")]
        public async Task<ActionResult<Response<UserProfileDto>>> UpdateMyProfile([FromBody] UpdateProfileDto dto)
        {
            var updatedProfile = await _profileService.UpdateUserProfileAsync(GetCurrentUserId(), dto);
            if (updatedProfile == null)
            {
                return NotFound(new Response<object> { Status = 404, Message = "User profile not found." });
            }
            return Ok(new Response<UserProfileDto> { Status = 200, Message = "Profile updated successfully.", Data = updatedProfile });
        }

        [HttpPut("me/phone-number")]
        public async Task<ActionResult<Response<object>>> UpdatePhoneNumber([FromBody] UpdatePhoneNumberDto dto)
        {
            var (success, message) = await _profileService.UpdatePhoneNumberAsync(GetCurrentUserId(), dto);

            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = message });
            }

            return Ok(new Response<object> { Status = 200, Message = message });
        }

        [HttpGet("me/bank")]
        public async Task<ActionResult<Response<PlayerBankDto>>> GetMyBankInfo()
        {
            var bankInfo = await _profileService.GetBankInfoAsync(GetCurrentUserId());
            if (bankInfo == null) return NotFound(new Response<object> { Status = 404, Message = "User profile not found." });
            return Ok(new Response<PlayerBankDto> { Status = 200, Message = "Success", Data = bankInfo });
        }

        [HttpPut("me/bank")]
        public async Task<ActionResult<Response<PlayerBankDto>>> UpdateMyBankInfo([FromBody] UpdatePlayerBankDto dto)
        {
            var updatedBank = await _profileService.UpdateBankInfoAsync(GetCurrentUserId(), dto);
            if (updatedBank == null) return NotFound(new Response<object> { Status = 404, Message = "User profile not found." });
            return Ok(new Response<PlayerBankDto> { Status = 200, Message = "Bank info updated successfully.", Data = updatedBank });
        }
    }
}