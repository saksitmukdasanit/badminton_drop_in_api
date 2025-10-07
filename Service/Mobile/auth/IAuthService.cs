using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IAuthService
    {
        Task<(bool Success, string ErrorMessage)> InitiateRegistrationAsync(InitiateRegisterDto dto);
        Task<(string? AccessToken, string? RefreshToken, string ErrorMessage)> VerifyOtpAndLoginAsync(VerifyOtpDto dto);
        Task<(bool Success, string ErrorMessage)> CompleteUserProfileAsync(int userId, CompleteProfileDto dto);

        Task<(string? AccessToken, string? RefreshToken)> LoginUserAsync(LoginDto loginDto);
        Task<UserProfileDto?> GetUserProfileAsync(int userId);
        Task<(string? AccessToken, string? RefreshToken)> RefreshTokenAsync(string accessToken, string refreshToken);
        Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);
        Task<bool> RequestPasswordResetOtpAsync(RequestOtpDto dto);
        Task<(string? ResetToken, string ErrorMessage)> VerifyPasswordResetOtpAsync(VerifyOtpDto dto);
        Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(int userId, ResetPasswordDto dto);

    }
}