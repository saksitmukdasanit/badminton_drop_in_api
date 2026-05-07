using System;

namespace DropInBadAPI.Models;

/// <summary>
/// Apple Guideline 1.2 — ผู้ใช้สามารถ block ผู้ใช้อื่นไม่ให้ติดต่อ/แสดงผลร่วม
/// </summary>
public partial class UserBlock
{
    public int BlockId { get; set; }

    public int BlockerUserId { get; set; }

    public int BlockedUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Blocker { get; set; } = null!;
    public virtual User Blocked { get; set; } = null!;
}
