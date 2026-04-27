using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using FirebaseAdmin.Messaging;
using System.Linq;
using System.Threading.Tasks;

namespace DropInBadAPI.Services
{
    public class NotificationService : INotificationService
    {
        private readonly BadmintonDbContext _context;

        public NotificationService(BadmintonDbContext context)
        {
            _context = context;
        }

        public async Task<List<NotificationDto>> GetUserNotificationsAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedDate)
                .Select(n => new NotificationDto
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type,
                    ReferenceId = n.ReferenceId,
                    IsRead = n.IsRead,
                    CreatedDate = n.CreatedDate
                })
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

            if (notification == null) return false;

            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            // อัปเดตเฉพาะอันที่ยังไม่อ่านให้เป็นอ่านแล้วให้หมดรวดเดียว
            await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

            return true;
        }

        public async Task SendNotificationAsync(int userId, string title, string message, string type, int? referenceId = null)
        {
            var notification = new DropInBadAPI.Models.Notification
            {
                UserId = userId, Title = title, Message = message, Type = type, ReferenceId = referenceId, IsRead = false, CreatedDate = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // ดึง FCM Token ของ User จากตาราง UserLogins (ใช้ ProviderName = "FCM")
            var fcmLogin = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.UserId == userId && ul.ProviderName == "FCM");

            if (fcmLogin != null && !string.IsNullOrEmpty(fcmLogin.ProviderKey))
            {
                try
                {
                    var msg = new FirebaseAdmin.Messaging.Message()
                    {
                        Token = fcmLogin.ProviderKey,
                        Notification = new FirebaseAdmin.Messaging.Notification
                        {
                            Title = title,
                            Body = message
                        },
                        Data = new Dictionary<string, string>
                        {
                            { "type", type },
                            { "referenceId", referenceId?.ToString() ?? "" }
                        }
                    };
                    await FirebaseMessaging.DefaultInstance.SendAsync(msg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Firebase FCM Error: {ex.Message}");
                }
            }
        }

        public async Task UpdateFcmTokenAsync(int userId, string token)
        {
            var fcmLogin = await _context.UserLogins.FirstOrDefaultAsync(ul => ul.UserId == userId && ul.ProviderName == "FCM");
            if (fcmLogin == null)
            {
                _context.UserLogins.Add(new UserLogin { UserId = userId, ProviderName = "FCM", ProviderKey = token, PasswordHash = "" });
            }
            else
            {
                fcmLogin.ProviderKey = token;
            }
            await _context.SaveChangesAsync();
        }
    }
}