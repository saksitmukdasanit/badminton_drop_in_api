using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models; // << ตรวจสอบว่า using Response ของคุณอยู่ที่นี่
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Mobile
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
        public async Task<ActionResult<Response<object>>> InitiateRegistration([FromBody] InitiateRegisterDto dto)
        {
            var (accessToken, refreshToken, errorMessage) = await _authService.RegisterAsync(dto);
            if (string.IsNullOrEmpty(accessToken))
            {
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }

            var data = new LoginResponseDto(accessToken, refreshToken!);
            return Ok(new Response<LoginResponseDto> { Status = 201, Message = "User registered and logged in successfully.", Data = data });
        }


        [HttpPut("complete-profile")]
        [Authorize]
        public async Task<ActionResult<Response<object>>> CompleteProfile([FromBody] CompleteProfileDto dto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, errorMessage) = await _authService.CompleteUserProfileAsync(int.Parse(userIdString!), dto);

            if (!success)
            {
                return NotFound(new Response<object> { Status = 404, Message = errorMessage });
            }

            return Ok(new Response<object> { Status = 200, Message = "Profile completed successfully." });
        }

        [HttpPost("login")]
        public async Task<ActionResult<Response<LoginResponseDto>>> Login([FromBody] LoginDto loginDto)
        {
            var (accessToken, refreshToken, errorMessage) = await _authService.LoginUserAsync(loginDto);
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized(new Response<object> { Status = 401, Message = errorMessage ?? "Invalid username or password." });
            }

            var data = new LoginResponseDto(accessToken, refreshToken);
            return Ok(new Response<LoginResponseDto> { Status = 200, Message = "Login successful.", Data = data });
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<Response<LoginResponseDto>>> Refresh([FromBody] RefreshTokenDto tokenDto)
        {
            var (newAccessToken, newRefreshToken) = await _authService.RefreshTokenAsync(tokenDto.AccessToken, tokenDto.RefreshToken);

            if (string.IsNullOrEmpty(newAccessToken) || string.IsNullOrEmpty(newRefreshToken))
            {
                return Unauthorized(new Response<object> { Status = 401, Message = "Invalid tokens." });
            }

            var data = new LoginResponseDto(newAccessToken, newRefreshToken);
            return Ok(new Response<LoginResponseDto> { Status = 200, Message = "Token refreshed successfully.", Data = data });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult<Response<object>>> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized();
            }

            var (success, errorMessage) = await _authService.ChangePasswordAsync(int.Parse(userIdString), changePasswordDto);

            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }

            return Ok(new Response<object> { Status = 200, Message = "Password changed successfully." });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<Response<UserProfileDto>>> GetMyProfile()
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
                return NotFound(new Response<object> { Status = 404, Message = "User profile not found." });
            }

            return Ok(new Response<UserProfileDto> { Status = 200, Message = "Profile retrieved successfully.", Data = userProfile });
        }

        [HttpPost("forgot-password/reset")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var (success, errorMessage) = await _authService.ResetPasswordAsync(dto);

            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = errorMessage });
            }
            return Ok(new Response<object> { Status = 200, Message = "Password has been reset successfully." });
        }

        // --- OTP Endpoints ---

        [HttpPost("verify-otp")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            var (success, message) = await _authService.VerifyOtpAsync(dto.PhoneNumber, dto.Otp);

            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = message });
            }
            return Ok(new Response<object> { Status = 200, Message = message });
        }

        [HttpPost("resend-otp")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDto dto)
        {
            var (success, message) = await _authService.ResendOtpAsync(dto.PhoneNumber);

            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = message });
            }
            return Ok(new Response<object> { Status = 200, Message = message });
        }

        // --- Social Login ---

        [HttpPost("login-google")]
        [AllowAnonymous]
        public async Task<ActionResult<Response<SocialLoginResponseDto>>> LoginWithGoogle([FromBody] GoogleLoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.IdToken))
            {
                return BadRequest(new Response<object> { Status = 400, Message = "Missing idToken." });
            }

            var (response, errorMessage) = await _authService.LoginWithGoogleAsync(dto);
            if (response == null)
            {
                return Unauthorized(new Response<object> { Status = 401, Message = errorMessage });
            }
            return Ok(new Response<SocialLoginResponseDto>
            {
                Status = 200,
                Message = response.RequiresPhoneVerification ? "Login successful, phone verification required." : "Login successful.",
                Data = response
            });
        }

        [HttpPost("login-apple")]
        [AllowAnonymous]
        public async Task<ActionResult<Response<SocialLoginResponseDto>>> LoginWithApple([FromBody] AppleLoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.IdentityToken))
            {
                return BadRequest(new Response<object> { Status = 400, Message = "Missing identityToken." });
            }

            var (response, errorMessage) = await _authService.LoginWithAppleAsync(dto);
            if (response == null)
            {
                return Unauthorized(new Response<object> { Status = 401, Message = errorMessage });
            }
            return Ok(new Response<SocialLoginResponseDto>
            {
                Status = 200,
                Message = response.RequiresPhoneVerification ? "Login successful, phone verification required." : "Login successful.",
                Data = response
            });
        }

        /// <summary>
        /// เชื่อมเบอร์โทรกับบัญชีที่ login ผ่าน social (ใช้หลัง social signup ครั้งแรก)
        /// — บันทึกเบอร์โทรลง UserProfile แล้วส่ง OTP ทันที. หลังจากนี้ผู้ใช้จะ verify ผ่าน /verify-otp endpoint เดิม
        /// </summary>
        [HttpPost("link-phone")]
        [Authorize]
        public async Task<IActionResult> LinkPhoneNumber([FromBody] LinkPhoneDto dto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized();
            }

            var (success, message) = await _authService.LinkPhoneNumberAsync(int.Parse(userIdString), dto.PhoneNumber);
            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = message });
            }
            return Ok(new Response<object> { Status = 200, Message = message });
        }

        // --- Account Deletion (Apple Guideline 5.1.1(v)) ---

        /// <summary>
        /// ขอลบบัญชี — soft-delete + 30-day grace period. หลังครบ 30 วันจะถูก hard-delete โดย background job
        /// </summary>
        [HttpPost("request-deletion")]
        [Authorize]
        public async Task<IActionResult> RequestAccountDeletion()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized();
            }

            var (success, message, scheduledAt) = await _authService.RequestAccountDeletionAsync(int.Parse(userIdString));
            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = message });
            }
            return Ok(new Response<AccountDeletionResponseDto>
            {
                Status = 200,
                Message = message,
                Data = new AccountDeletionResponseDto(scheduledAt!.Value)
            });
        }

        /// <summary>
        /// กู้คืนบัญชีในระยะ 30 วัน
        /// </summary>
        [HttpPost("cancel-deletion")]
        [Authorize]
        public async Task<IActionResult> CancelAccountDeletion()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized();
            }

            var (success, message) = await _authService.CancelAccountDeletionAsync(int.Parse(userIdString));
            if (!success)
            {
                return BadRequest(new Response<object> { Status = 400, Message = message });
            }
            return Ok(new Response<object> { Status = 200, Message = message });
        }
    }

    public record AccountDeletionResponseDto(DateTime ScheduledForDeletionAt);

    // DTOs สำหรับ OTP (ใส่ไว้ในไฟล์เดียวกันหรือแยกไฟล์ก็ได้)
    public record VerifyOtpDto(string PhoneNumber, string Otp);
    public record ResendOtpDto(string PhoneNumber);
    public record LinkPhoneDto(string PhoneNumber);
}