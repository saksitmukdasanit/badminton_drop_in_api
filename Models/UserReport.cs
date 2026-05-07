using System;

namespace DropInBadAPI.Models;

/// <summary>
/// Apple Guideline 5.1.1(viii) — กลไกให้ผู้ใช้รายงานพฤติกรรมไม่เหมาะสม
/// </summary>
public partial class UserReport
{
    public int ReportId { get; set; }

    public int ReporterUserId { get; set; }

    public int ReportedUserId { get; set; }

    /// <summary>
    /// รหัสเหตุผล: spam, harassment, fraud, fake_profile, inappropriate_content, other
    /// </summary>
    public string Reason { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// (optional) อ้างอิง session/match ที่เกิดเหตุ
    /// </summary>
    public int? SessionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public int? ResolvedByUserId { get; set; }

    public string? AdminNotes { get; set; }

    public virtual User Reporter { get; set; } = null!;
    public virtual User Reported { get; set; } = null!;
}
