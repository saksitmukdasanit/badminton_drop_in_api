using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> InitiateRegistration([FromBody] InitiateRegisterDto dto)
        {
            var (success, errorMessage) = await _authService.InitiateRegistrationAsync(dto);
            if (!success) return BadRequest(errorMessage);
            return Ok("Registration initiated. Please verify OTP.");
        }

        [HttpPost("verify-otp")]
        public async Task<ActionResult<LoginResponseDto>> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            var (accessToken, refreshToken, errorMessage) = await _authService.VerifyOtpAndLoginAsync(dto);
            if (string.IsNullOrEmpty(accessToken)) return BadRequest(errorMessage);
            return Ok(new LoginResponseDto(accessToken, refreshToken!));
        }

        [HttpPut("complete-profile")]
        [Authorize] // ต้องใช้ Token จากขั้นตอนที่ 2 ถึงจะเรียกได้
        public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileDto dto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, errorMessage) = await _authService.CompleteUserProfileAsync(int.Parse(userIdString!), dto);

            if (!success) return NotFound(errorMessage);

            return Ok("Profile completed successfully.");
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            var (accessToken, refreshToken) = await _authService.LoginUserAsync(loginDto);
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized("Invalid username or password.");
            }

            return Ok(new LoginResponseDto(accessToken, refreshToken));
        }

        // Endpoint นี้จะยังเรียกใช้ไม่ได้จนกว่าจะใส่ระบบ JWT


        [HttpPost("refresh")]
        public async Task<ActionResult<LoginResponseDto>> Refresh([FromBody] RefreshTokenDto tokenDto)
        {
            var (newAccessToken, newRefreshToken) = await _authService.RefreshTokenAsync(tokenDto.AccessToken, tokenDto.RefreshToken);

            if (string.IsNullOrEmpty(newAccessToken) || string.IsNullOrEmpty(newRefreshToken))
            {
                return Unauthorized("Invalid tokens.");
            }

            return Ok(new LoginResponseDto(newAccessToken, newRefreshToken));
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized();
            }

            var (success, errorMessage) = await _authService.ChangePasswordAsync(int.Parse(userIdString), changePasswordDto);

            if (!success)
            {
                return BadRequest(errorMessage);
            }

            return Ok("Password changed successfully.");
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized();
            }

            var userId = int.Parse(userIdString);
            var userProfile = await _authService.GetUserProfileAsync(userId);

            if (userProfile == null)
            {
                return NotFound();
            }

            return Ok(userProfile);
        }

        [HttpPost("forgot-password/request-otp")]
        [AllowAnonymous] // ทุกคนต้องเรียกได้
        public async Task<IActionResult> RequestPasswordReset([FromBody] RequestOtpDto dto)
        {
            await _authService.RequestPasswordResetOtpAsync(dto);
            // คืนค่า 200 OK เสมอ ไม่ว่าเบอร์โทรจะถูกหรือผิด เพื่อความปลอดภัย
            return Ok("If a matching account was found, an OTP has been sent.");
        }

        [HttpPost("forgot-password/verify-otp")]
        [AllowAnonymous]
        public async Task<ActionResult<ResetTokenResponseDto>> VerifyPasswordResetOtp([FromBody] VerifyOtpDto dto)
        {
            var (resetToken, errorMessage) = await _authService.VerifyPasswordResetOtpAsync(dto);
            if (string.IsNullOrEmpty(resetToken))
            {
                return BadRequest(errorMessage);
            }
            return Ok(new ResetTokenResponseDto(resetToken));
        }

        [HttpPost("forgot-password/reset")]
        [Authorize] // ต้องใช้ "Reset Token" ที่ได้จากขั้นตอนที่แล้วถึงจะเรียกได้
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, errorMessage) = await _authService.ResetPasswordAsync(int.Parse(userIdString!), dto);

            if (!success)
            {
                return BadRequest(errorMessage);
            }
            return Ok("Password has been reset successfully.");
        }
    }
}