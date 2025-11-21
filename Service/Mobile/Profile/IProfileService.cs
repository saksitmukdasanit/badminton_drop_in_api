using DropInBadAPI.Dtos;

namespace DropInBadAPI.Service.Mobile.Profile
{
    public interface IProfileService
    {
        Task<UserProfileDto?> GetUserProfileAsync(int userId);
        Task<UserProfileDto?> UpdateUserProfileAsync(int userId, UpdateProfileDto dto);
        Task<(bool Success, string ErrorMessage)> UpdatePhoneNumberAsync(int userId, UpdatePhoneNumberDto dto);
    }
}