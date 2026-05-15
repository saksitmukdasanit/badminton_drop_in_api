using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models; // ใช้สำหรับคลาส Response<T>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DropInBadAPI.Controllers.Mobile
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<Response<List<NotificationDto>>>> GetMyNotifications()
        {
            var notifications = await _notificationService.GetUserNotificationsAsync(GetCurrentUserId());
            return Ok(new Response<List<NotificationDto>> { Status = 200, Message = "Success", Data = notifications });
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<Response<int>>> GetUnreadCount()
        {
            var count = await _notificationService.GetUnreadCountAsync(GetCurrentUserId());
            return Ok(new Response<int> { Status = 200, Message = "Success", Data = count });
        }

        [HttpPut("{id}/read")]
        public async Task<ActionResult<Response<object>>> MarkAsRead(int id)
        {
            var success = await _notificationService.MarkAsReadAsync(id, GetCurrentUserId());
            if (!success) return NotFound(new Response<object> { Status = 404, Message = "Notification not found." });
            return Ok(new Response<object> { Status = 200, Message = "Marked as read." });
        }

        [HttpPut("read-all")]
        public async Task<ActionResult<Response<object>>> MarkAllAsRead()
        {
            await _notificationService.MarkAllAsReadAsync(GetCurrentUserId());
            return Ok(new Response<object> { Status = 200, Message = "All notifications marked as read." });
        }

        [HttpDelete("delete-all")]
        public async Task<ActionResult<Response<object>>> DeleteAllNotifications()
        {
            await _notificationService.DeleteAllNotificationsAsync(GetCurrentUserId());
            return Ok(new Response<object> { Status = 200, Message = "All notifications deleted successfully." });
        }

        [HttpPost("fcm-token")]
        public async Task<ActionResult<Response<object>>> UpdateFcmToken([FromBody] UpdateFcmTokenDto dto)
        {
            await _notificationService.UpdateFcmTokenAsync(GetCurrentUserId(), dto.Token);
            return Ok(new Response<object> { Status = 200, Message = "FCM Token updated successfully." });
        }

        [HttpPost("fcm-token/unregister")]
        public async Task<ActionResult<Response<object>>> UnregisterFcmToken([FromBody] UpdateFcmTokenDto dto)
        {
            await _notificationService.RemoveFcmTokenAsync(GetCurrentUserId(), dto.Token);
            return Ok(new Response<object> { Status = 200, Message = "FCM token removed." });
        }
    }
}