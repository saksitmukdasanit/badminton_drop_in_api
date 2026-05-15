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
        Task DeleteAllNotificationsAsync(int userId);
        Task SendNotificationAsync(int userId, string title, string message, string type, int? referenceId = null);
        /// <summary>ส่ง FCM เท่านั้น (ใช้หลังบันทึกแถว Notification แล้ว เช่น CMS)</summary>
        Task DispatchFirebaseForUserAsync(int userId, string title, string message, string type, int? referenceId = null);
        Task UpdateFcmTokenAsync(int userId, string token);
        /// <summary>ลบ FCM token ของเครื่องนี้ออกจาก user (เรียกตอน logout)</summary>
        Task RemoveFcmTokenAsync(int userId, string token);
    }
}