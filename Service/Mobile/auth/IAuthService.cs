using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IAuthService
    {
        Task<(string? AccessToken, string? RefreshToken, string ErrorMessage)> RegisterAsync(InitiateRegisterDto dto);

        Task<(bool Success, string ErrorMessage)> CompleteUserProfileAsync(int userId, CompleteProfileDto dto);

        Task<(string? AccessToken, string? RefreshToken)> LoginUserAsync(LoginDto loginDto);
        Task<UserProfileDto?> GetUserProfileAsync(int userId);
        Task<(string? AccessToken, string? RefreshToken)> RefreshTokenAsync(string accessToken, string refreshToken);
        Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);
        Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(ResetPasswordDto dto);


    }
}