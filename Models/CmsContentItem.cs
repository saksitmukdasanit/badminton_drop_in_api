namespace DropInBadAPI.Models;

public static class CmsContentTypes
{
    public const short SplashScreen = 1;
    public const short Banner = 2;
    public const short MainPopup = 3;
}

public static class CmsContentPlatforms
{
    public const short All = 0;
    public const short Ios = 1;
    public const short Android = 2;
}

public class CmsContentItem
{
    public int CmsContentItemId { get; set; }

    /// <summary>1=Splash 2=Banner 3=MainPopup</summary>
    public short ContentType { get; set; }

    public string? Title { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public string? LinkUrl { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? ValidFromUtc { get; set; }

    public DateTime? ValidToUtc { get; set; }

    public short Platform { get; set; }

    public string? ExtraJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int? CreatedByCmsAdminUserId { get; set; }

    public CmsAdminUser? CreatedBy { get; set; }
}
