using System;

namespace DropInBadAPI.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string Type { get; set; } = null!;
        public int? ReferenceId { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }

        // Navigation property (ไม่จำเป็นต้องใส่กลับไปใน User.cs ก็ทำงานได้ด้วย Entity.HasOne)
        public virtual User User { get; set; } = null!;
    }
}