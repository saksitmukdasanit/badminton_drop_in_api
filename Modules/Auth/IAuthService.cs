using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IAuthService
    {
        Task<(string? AccessToken, string? RefreshToken, string ErrorMessage)> RegisterAsync(InitiateRegisterDto dto);

        Task<(bool Success, string ErrorMessage)> CompleteUserProfileAsync(int userId, CompleteProfileDto dto);

        Task<(string? AccessToken, string? RefreshToken, string ErrorMessage)> LoginUserAsync(LoginDto loginDto);
        Task<UserProfileDto?> GetUserProfileAsync(int userId);
        Task<(string? AccessToken, string? RefreshToken)> RefreshTokenAsync(string accessToken, string refreshToken);
        Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);
        Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(ResetPasswordDto dto);
        Task<(bool Success, string Message)> VerifyOtpAsync(string phoneNumber, string otp);
        Task<(bool Success, string Message)> ResendOtpAsync(string phoneNumber, int? forUserId = null);
        Task<(SocialLoginResponseDto? Response, string ErrorMessage)> LoginWithGoogleAsync(GoogleLoginDto dto);
        Task<(SocialLoginResponseDto? Response, string ErrorMessage)> LoginWithAppleAsync(AppleLoginDto dto);
        Task<(bool Success, string Message)> LinkPhoneNumberAsync(int userId, string phoneNumber);
        Task<(bool Success, string Message, DateTime? scheduledDeletionAt)> RequestAccountDeletionAsync(int userId);
        Task<(bool Success, string Message)> CancelAccountDeletionAsync(int userId);
    }
}