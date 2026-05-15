namespace DropInBadAPI.Models;

/// <summary>ข้อความหน้า «เกี่ยวกับเรา» (แถวเดียว id=1)</summary>
public class CmsAboutSettings
{
    public short CmsAboutSettingsId { get; set; } = 1;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }
}
