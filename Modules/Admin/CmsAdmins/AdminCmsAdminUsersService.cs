using DropInBadAPI.Data;
using DropInBadAPI.Models;
using DropInBadAPI.Modules.Auth;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public interface IAdminCmsAdminUsersService
{
    Task<(List<CmsAdminUserListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize);
    Task<CmsAdminUserListItemDto?> GetByIdAsync(int id);
    Task<(CmsAdminUserListItemDto? Data, string Error)> CreateAsync(CmsAdminUserCreateDto dto);
    Task<(CmsAdminUserListItemDto? Data, string Error)> UpdateAsync(int id, CmsAdminUserUpdateDto dto, int actingAdminId);
    Task<(bool Ok, string Error)> DeactivateAsync(int id, int actingAdminId);
}

public class AdminCmsAdminUsersService : IAdminCmsAdminUsersService
{
    private readonly BadmintonDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AdminCmsAdminUsersService(BadmintonDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<(List<CmsAdminUserListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.CmsAdminUsers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var pattern = $"%{term}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.Email, pattern)
                || (x.DisplayName != null && EF.Functions.ILike(x.DisplayName, pattern)));
        }

        var total = await q.LongCountAsync();
        var rows = await q
            .OrderBy(x => x.CmsAdminUserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CmsAdminUserListItemDto(
                x.CmsAdminUserId,
                x.Email,
                x.DisplayName,
                x.IsActive,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync();

        return (rows, total);
    }

    public async Task<CmsAdminUserListItemDto?> GetByIdAsync(int id)
    {
        var x = await _db.CmsAdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.CmsAdminUserId == id);
        return x == null
            ? null
            : new CmsAdminUserListItemDto(x.CmsAdminUserId, x.Email, x.DisplayName, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc);
    }

    public async Task<(CmsAdminUserListItemDto? Data, string Error)> CreateAsync(CmsAdminUserCreateDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(dto.Password))
        {
            return (null, "อีเมลและรหัสผ่านจำเป็น");
        }

        if (await _db.CmsAdminUsers.AnyAsync(a => a.Email.ToLower() == email))
        {
            return (null, "อีเมลนี้มีแล้ว");
        }

        var now = DateTime.UtcNow;
        var entity = new CmsAdminUser
        {
            Email = email,
            PasswordHash = _hasher.Hash(dto.Password),
            DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.CmsAdminUsers.Add(entity);
        await _db.SaveChangesAsync();

        return (new CmsAdminUserListItemDto(entity.CmsAdminUserId, entity.Email, entity.DisplayName, entity.IsActive, entity.CreatedAtUtc,
            entity.UpdatedAtUtc), string.Empty);
    }

    public async Task<(CmsAdminUserListItemDto? Data, string Error)> UpdateAsync(int id, CmsAdminUserUpdateDto dto, int actingAdminId)
    {
        var x = await _db.CmsAdminUsers.FirstOrDefaultAsync(a => a.CmsAdminUserId == id);
        if (x == null)
        {
            return (null, "ไม่พบบัญชี");
        }

        if (!dto.IsActive && x.IsActive)
        {
            if (id == actingAdminId)
            {
                return (null, "ไม่สามารถปิดการใช้งานบัญชีที่กำลังล็อกอินอยู่");
            }

            var otherActive = await _db.CmsAdminUsers.CountAsync(a => a.IsActive && a.CmsAdminUserId != id);
            if (otherActive < 1)
            {
                return (null, "ต้องมีแอดมินที่ใช้งานอยู่อย่างน้อย 1 บัญชี");
            }
        }

        x.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName!.Trim();
        x.IsActive = dto.IsActive;
        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            x.PasswordHash = _hasher.Hash(dto.NewPassword!);
        }

        x.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (new CmsAdminUserListItemDto(x.CmsAdminUserId, x.Email, x.DisplayName, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc),
            string.Empty);
    }

    public async Task<(bool Ok, string Error)> DeactivateAsync(int id, int actingAdminId)
    {
        var x = await _db.CmsAdminUsers.FirstOrDefaultAsync(a => a.CmsAdminUserId == id);
        if (x == null)
        {
            return (false, "ไม่พบบัญชี");
        }

        if (!x.IsActive)
        {
            return (true, string.Empty);
        }

        if (id == actingAdminId)
        {
            return (false, "ไม่สามารถปิดการใช้งานบัญชีที่กำลังล็อกอินอยู่");
        }

        var otherActive = await _db.CmsAdminUsers.CountAsync(a => a.IsActive && a.CmsAdminUserId != id);
        if (otherActive < 1)
        {
            return (false, "ต้องมีแอดมินที่ใช้งานอยู่อย่างน้อย 1 บัญชี");
        }

        x.IsActive = false;
        x.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }
}

