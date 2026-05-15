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
using Microsoft.Extensions.DependencyInjection; // ต้องใช้สำหรับ IServiceScopeFactory

namespace DropInBadAPI.Services
{
    public class NotificationService : INotificationService
    {
        private readonly BadmintonDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;

        public NotificationService(BadmintonDbContext context, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _scopeFactory = scopeFactory;
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

        public async Task DeleteAllNotificationsAsync(int userId)
        {
            await _context.Notifications
                .Where(n => n.UserId == userId)
                .ExecuteDeleteAsync();
        }

        public async Task SendNotificationAsync(int userId, string title, string message, string type, int? referenceId = null)
        {
            // --- Fire-and-forget: โยนเข้า Background Thread ให้ทำงานเงียบๆ ---
            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine($"[FCM-BG] เริ่มประมวลผลส่ง Noti ให้ UserID: {userId}");
                    // สร้าง Scope ใหม่เพื่อดึง DbContext ที่เป็นอิสระจาก HTTP Request เดิม
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<BadmintonDbContext>();

                    var notification = new DropInBadAPI.Models.Notification
                    {
                        UserId = userId, Title = title, Message = message, Type = type, ReferenceId = referenceId, IsRead = false, CreatedDate = DateTime.UtcNow
                    };
                    dbContext.Notifications.Add(notification);
                    await dbContext.SaveChangesAsync(); // บันทึกข้อมูลแบบไม่รบกวน Thread หลัก
                    Console.WriteLine($"[FCM-BG] บันทึก Noti ลง Database สำเร็จ (UserID: {userId})");

                    // --- ดึง Token ทุกเครื่องของ User คนนี้ขึ้นมา ---
                    var fcmTokens = await dbContext.UserFcmTokens
                        .Where(t => t.UserId == userId)
                        .ToListAsync();

                    if (fcmTokens.Any())
                    {
                        Console.WriteLine($"[FCM-BG] พบ FCM Token ของ UserID: {userId} จำนวน {fcmTokens.Count} เครื่อง");
                        
                        var tokensToDelete = new List<UserFcmToken>();

                        foreach (var fcmToken in fcmTokens)
                        {
                            var msg = new FirebaseAdmin.Messaging.Message()
                            {
                                Token = fcmToken.Token,
                                Notification = new FirebaseAdmin.Messaging.Notification
                                {
                                    Title = title,
                                    Body = message
                                },
                                // บังคับให้แจ้งเตือนทันที (ทะลุโหมดประหยัดแบต)
                                Android = new AndroidConfig { Priority = Priority.High },
                                Apns = new ApnsConfig { Aps = new Aps { Sound = "default" } },
                                Data = new Dictionary<string, string>
                                {
                                    { "type", type },
                                    { "referenceId", referenceId?.ToString() ?? "" }
                                }
                            };
                            
                            try
                            {
                                await FirebaseMessaging.DefaultInstance.SendAsync(msg);
                                Console.WriteLine($"[FCM-BG] ส่ง FCM สำเร็จ (Token ID: {fcmToken.TokenId})");
                            }
                            catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered || ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                            {
                                // --- Passive Cleanup: ถ้า Token ตายแล้ว ให้เก็บไว้เตรียมลบ ---
                                tokensToDelete.Add(fcmToken);
                                Console.WriteLine($"[FCM-BG-WARN] FCM Token ตาย/ถูกยกเลิก (Token ID: {fcmToken.TokenId})");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[FCM-BG-ERROR] เกิดข้อผิดพลาดขณะส่ง FCM (Token ID: {fcmToken.TokenId}): {ex.Message}");
                            }
                        }

                        if (tokensToDelete.Any())
                        {
                            dbContext.UserFcmTokens.RemoveRange(tokensToDelete);
                            await dbContext.SaveChangesAsync();
                            Console.WriteLine($"[FCM-BG-WARN] ลบ FCM Token ขยะออกจากระบบจำนวน {tokensToDelete.Count} รายการ");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[FCM-BG-WARN] ไม่พบ FCM Token ใน Database สำหรับ UserID: {userId} ระบบจึงไม่สามารถส่ง Noti ได้");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FCM-BG-ERROR] เกิดข้อผิดพลาดขณะส่ง Firebase FCM (UserID {userId}): {ex.Message}");
                    if (ex.InnerException != null) Console.WriteLine($"   InnerException: {ex.InnerException.Message}");
                }
            });

            // ให้ Method หลักทำงานเสร็จทันที (ไม่เกิน 1 มิลลิวินาที)
            await Task.CompletedTask;
        }

        public async Task DispatchFirebaseForUserAsync(int userId, string title, string message, string type, int? referenceId = null)
        {
            var fcmTokens = await _context.UserFcmTokens
                .Where(t => t.UserId == userId)
                .ToListAsync();

            if (!fcmTokens.Any())
            {
                Console.WriteLine($"[FCM] ไม่พบ FCM Token สำหรับ UserID: {userId}");
                return;
            }

            var tokensToDelete = new List<UserFcmToken>();

            foreach (var fcmToken in fcmTokens)
            {
                var msg = new FirebaseAdmin.Messaging.Message()
                {
                    Token = fcmToken.Token,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = title,
                        Body = message
                    },
                    Android = new AndroidConfig { Priority = Priority.High },
                    Apns = new ApnsConfig { Aps = new Aps { Sound = "default" } },
                    Data = new Dictionary<string, string>
                    {
                        { "type", type },
                        { "referenceId", referenceId?.ToString() ?? "" }
                    }
                };

                try
                {
                    await FirebaseMessaging.DefaultInstance.SendAsync(msg);
                }
                catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered || ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                {
                    tokensToDelete.Add(fcmToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FCM-ERROR] UserID {userId} TokenId {fcmToken.TokenId}: {ex.Message}");
                }
            }

            if (tokensToDelete.Any())
            {
                _context.UserFcmTokens.RemoveRange(tokensToDelete);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateFcmTokenAsync(int userId, string token)
        {
            // เช็คว่ามี Token ในระบบอยู่แล้วหรือไม่ (ไม่สนใจว่าเป็นของใคร)
            var existingToken = await _context.UserFcmTokens.FirstOrDefaultAsync(t => t.Token == token);
            
            if (existingToken == null)
            {
                // ไม่เคยมีในระบบ -> สร้างใหม่
                _context.UserFcmTokens.Add(new UserFcmToken 
                { 
                    UserId = userId, 
                    Token = token, 
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow 
                });
            }
            else
            {
                // เคยมีแล้ว -> เปลี่ยนเจ้าของเป็นคนล่าสุด (เผื่อมีคนสลับบัญชีล็อกอินบนเครื่องเดิม) และอัปเดตเวลา
                if (existingToken.UserId != userId)
                {
                    existingToken.UserId = userId;
                }
                existingToken.UpdatedDate = DateTime.UtcNow;
            }

            // --- Active Cleanup: ลบ Token ของ User คนนี้ที่เก่าเกิน 180 วัน (6 เดือน) ---
            var expirationDate = DateTime.UtcNow.AddDays(-180);
            var expiredTokens = await _context.UserFcmTokens
                .Where(t => t.UserId == userId && t.UpdatedDate < expirationDate)
                .ToListAsync();
            
            if (expiredTokens.Any())
            {
                _context.UserFcmTokens.RemoveRange(expiredTokens);
                Console.WriteLine($"[FCM-CLEANUP] ลบ Token หมดอายุของ UserID: {userId} จำนวน {expiredTokens.Count} รายการ");
            }

            // --- ทำความสะอาด (Migration): ลบ Token ที่เคยเซฟผิดไว้ในตาราง UserLogins ทิ้ง ---
            var oldLogins = await _context.UserLogins.Where(ul => ul.UserId == userId && ul.ProviderName == "FCM").ToListAsync();
            if (oldLogins.Any()) _context.UserLogins.RemoveRange(oldLogins);

            await _context.SaveChangesAsync();
        }

        public async Task RemoveFcmTokenAsync(int userId, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;

            var row = await _context.UserFcmTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token);
            if (row == null) return;

            _context.UserFcmTokens.Remove(row);
            await _context.SaveChangesAsync();
        }
    }
}