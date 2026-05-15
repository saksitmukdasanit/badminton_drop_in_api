using System.Text.Json;
using DropInBadAPI.Data;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public interface IAdminCmsService
{
    Task<(List<CmsContentItemDto> Items, long Total)> ListPagedAsync(short? contentType, int page, int pageSize);
    Task<CmsContentItemDto?> GetByIdAsync(int id);
    Task<(CmsContentItemDto? Data, string Error)> CreateAsync(int cmsAdminUserId, CmsContentItemCreateDto dto, string? ipAddress);
    Task<(CmsContentItemDto? Data, string Error)> UpdateAsync(int cmsAdminUserId, int id, CmsContentItemUpdateDto dto, string? ipAddress);
    Task<(bool Ok, string Error)> DeleteAsync(int cmsAdminUserId, int id, string? ipAddress);
}

public class AdminCmsService : IAdminCmsService
{
    private readonly BadmintonDbContext _db;
    private readonly IAdminAuditService _audit;

    public AdminCmsService(BadmintonDbContext db, IAdminAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<(List<CmsContentItemDto> Items, long Total)> ListPagedAsync(short? contentType, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.CmsContentItems.AsNoTracking().AsQueryable();
        if (contentType.HasValue)
        {
            q = q.Where(x => x.ContentType == contentType.Value);
        }

        var total = await q.LongCountAsync();
        var items = await q
            .OrderBy(x => x.ContentType)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.CmsContentItemId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items.Select(Map).ToList(), total);
    }

    public async Task<CmsContentItemDto?> GetByIdAsync(int id)
    {
        var e = await _db.CmsContentItems.AsNoTracking().FirstOrDefaultAsync(x => x.CmsContentItemId == id);
        return e == null ? null : Map(e);
    }

    public async Task<(CmsContentItemDto? Data, string Error)> CreateAsync(int cmsAdminUserId, CmsContentItemCreateDto dto, string? ipAddress)
    {
        if (!IsValidType(dto.ContentType)) return (null, "ContentType ไม่ถูกต้อง (1=Splash 2=Banner 3=Popup)");
        if (string.IsNullOrWhiteSpace(dto.ImageUrl)) return (null, "ImageUrl จำเป็น");

        var now = DateTime.UtcNow;
        var entity = new CmsContentItem
        {
            ContentType = dto.ContentType,
            Title = dto.Title,
            ImageUrl = dto.ImageUrl.Trim(),
            LinkUrl = string.IsNullOrWhiteSpace(dto.LinkUrl) ? null : dto.LinkUrl.Trim(),
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
            ValidFromUtc = dto.ValidFromUtc,
            ValidToUtc = dto.ValidToUtc,
            Platform = dto.Platform,
            ExtraJson = dto.ExtraJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByCmsAdminUserId = cmsAdminUserId
        };
        _db.CmsContentItems.Add(entity);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(cmsAdminUserId, "cms.content.create", "CmsContentItem", entity.CmsContentItemId.ToString(),
            JsonSerializer.Serialize(new { dto.ContentType, dto.Title, dto.ImageUrl }), ipAddress);

        return (Map(entity), string.Empty);
    }

    public async Task<(CmsContentItemDto? Data, string Error)> UpdateAsync(int cmsAdminUserId, int id, CmsContentItemUpdateDto dto, string? ipAddress)
    {
        var entity = await _db.CmsContentItems.FirstOrDefaultAsync(x => x.CmsContentItemId == id);
        if (entity == null) return (null, "ไม่พบรายการ");
        if (string.IsNullOrWhiteSpace(dto.ImageUrl)) return (null, "ImageUrl จำเป็น");

        entity.Title = dto.Title;
        entity.ImageUrl = dto.ImageUrl.Trim();
        entity.LinkUrl = string.IsNullOrWhiteSpace(dto.LinkUrl) ? null : dto.LinkUrl.Trim();
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        entity.ValidFromUtc = dto.ValidFromUtc;
        entity.ValidToUtc = dto.ValidToUtc;
        entity.Platform = dto.Platform;
        entity.ExtraJson = dto.ExtraJson;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(cmsAdminUserId, "cms.content.update", "CmsContentItem", id.ToString(),
            JsonSerializer.Serialize(dto), ipAddress);

        return (Map(entity), string.Empty);
    }

    public async Task<(bool Ok, string Error)> DeleteAsync(int cmsAdminUserId, int id, string? ipAddress)
    {
        var entity = await _db.CmsContentItems.FirstOrDefaultAsync(x => x.CmsContentItemId == id);
        if (entity == null) return (false, "ไม่พบรายการ");

        _db.CmsContentItems.Remove(entity);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(cmsAdminUserId, "cms.content.delete", "CmsContentItem", id.ToString(), null, ipAddress);
        return (true, string.Empty);
    }

    private static bool IsValidType(short t) =>
        t is CmsContentTypes.SplashScreen or CmsContentTypes.Banner or CmsContentTypes.MainPopup;

    private static CmsContentItemDto Map(CmsContentItem e) =>
        new(
            e.CmsContentItemId,
            e.ContentType,
            e.Title,
            e.ImageUrl,
            e.LinkUrl,
            e.SortOrder,
            e.IsActive,
            e.ValidFromUtc,
            e.ValidToUtc,
            e.Platform,
            e.ExtraJson,
            e.CreatedAtUtc,
            e.UpdatedAtUtc);
}

