using DropInBadAPI.Data;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public interface IAdminPolicyDocumentsService
{
    Task<(List<CmsPolicyDocumentDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize);
    Task<CmsPolicyDocumentDto?> GetByIdAsync(int id);
    Task<(CmsPolicyDocumentDto? Data, string Error)> CreateAsync(CmsPolicyDocumentCreateDto dto);
    Task<(CmsPolicyDocumentDto? Data, string Error)> UpdateAsync(int id, CmsPolicyDocumentUpdateDto dto);
    Task<(bool Ok, string Error)> DeleteAsync(int id);
}

public class AdminPolicyDocumentsService : IAdminPolicyDocumentsService
{
    private readonly BadmintonDbContext _db;

    public AdminPolicyDocumentsService(BadmintonDbContext db)
    {
        _db = db;
    }

    public async Task<(List<CmsPolicyDocumentDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.CmsPolicyDocuments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.Title, pattern)
                || EF.Functions.ILike(x.Slug, pattern));
        }

        var total = await q.LongCountAsync();
        var items = await q
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CmsPolicyDocumentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CmsPolicyDocumentDto(
                x.CmsPolicyDocumentId,
                x.Title,
                x.Slug,
                x.Body,
                x.SortOrder,
                x.IsActive,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync();

        return (items, total);
    }

    public async Task<CmsPolicyDocumentDto?> GetByIdAsync(int id)
    {
        var x = await _db.CmsPolicyDocuments.AsNoTracking().FirstOrDefaultAsync(a => a.CmsPolicyDocumentId == id);
        return x == null
            ? null
            : new CmsPolicyDocumentDto(x.CmsPolicyDocumentId, x.Title, x.Slug, x.Body, x.SortOrder, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc);
    }

    public async Task<(CmsPolicyDocumentDto? Data, string Error)> CreateAsync(CmsPolicyDocumentCreateDto dto)
    {
        var slug = dto.Slug.Trim().ToLowerInvariant();
        if (await _db.CmsPolicyDocuments.AnyAsync(a => a.Slug == slug))
        {
            return (null, "Slug ซ้ำ");
        }

        var now = DateTime.UtcNow;
        var e = new CmsPolicyDocument
        {
            Title = dto.Title.Trim(),
            Slug = slug,
            Body = dto.Body ?? string.Empty,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.CmsPolicyDocuments.Add(e);
        await _db.SaveChangesAsync();

        return (new CmsPolicyDocumentDto(e.CmsPolicyDocumentId, e.Title, e.Slug, e.Body, e.SortOrder, e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc),
            string.Empty);
    }

    public async Task<(CmsPolicyDocumentDto? Data, string Error)> UpdateAsync(int id, CmsPolicyDocumentUpdateDto dto)
    {
        var e = await _db.CmsPolicyDocuments.FirstOrDefaultAsync(a => a.CmsPolicyDocumentId == id);
        if (e == null)
        {
            return (null, "ไม่พบรายการ");
        }

        var slug = dto.Slug.Trim().ToLowerInvariant();
        if (await _db.CmsPolicyDocuments.AnyAsync(a => a.Slug == slug && a.CmsPolicyDocumentId != id))
        {
            return (null, "Slug ซ้ำ");
        }

        e.Title = dto.Title.Trim();
        e.Slug = slug;
        e.Body = dto.Body ?? string.Empty;
        e.SortOrder = dto.SortOrder;
        e.IsActive = dto.IsActive;
        e.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (new CmsPolicyDocumentDto(e.CmsPolicyDocumentId, e.Title, e.Slug, e.Body, e.SortOrder, e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc),
            string.Empty);
    }

    public async Task<(bool Ok, string Error)> DeleteAsync(int id)
    {
        var e = await _db.CmsPolicyDocuments.FirstOrDefaultAsync(a => a.CmsPolicyDocumentId == id);
        if (e == null)
        {
            return (false, "ไม่พบรายการ");
        }

        _db.CmsPolicyDocuments.Remove(e);
        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }
}

