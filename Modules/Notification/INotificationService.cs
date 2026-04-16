using DropInBadAPI.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DropInBadAPI.Interfaces
{
    public interface INotificationService
    {
        Task<List<NotificationDto>> GetUserNotificationsAsync(int userId);
        Task<int> GetUnreadCountAsync(int userId);
        Task<bool> MarkAsReadAsync(int notificationId, int userId);
        Task<bool> MarkAllAsReadAsync(int userId);
        Task SendNotificationAsync(int userId, string title, string message, string type, int? referenceId = null);
    }
}