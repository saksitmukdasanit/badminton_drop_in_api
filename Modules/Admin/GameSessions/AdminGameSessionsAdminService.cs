using DropInBadAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public interface IAdminGameSessionsAdminService
{
    Task<(List<GameSessionAdminListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize);
    Task<GameSessionAdminDetailDto?> GetByIdAsync(int sessionId);
    Task<(GameSessionAdminDetailDto? Data, string Error)> UpdateAsync(int sessionId, GameSessionAdminUpdateDto dto);
    Task<(bool Ok, string Error)> CancelAsync(int sessionId);
}

public class AdminGameSessionsAdminService : IAdminGameSessionsAdminService
{
    private readonly BadmintonDbContext _db;

    public AdminGameSessionsAdminService(BadmintonDbContext db)
    {
        _db = db;
    }

    public async Task<(List<GameSessionAdminListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q =
            from s in _db.GameSessions.AsNoTracking()
            join v in _db.Venues.AsNoTracking() on s.VenueId equals v.VenueId
            join u in _db.Users.AsNoTracking() on s.CreatedByUserId equals u.UserId
            join p in _db.UserProfiles.AsNoTracking() on u.UserId equals p.UserId into pj
            from p in pj.DefaultIfEmpty()
            select new { s, v, p };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.s.GroupName, pattern)
                             || (x.p != null && EF.Functions.ILike(x.p.Nickname ?? "", pattern)));
        }

        var total = await q.LongCountAsync();
        var rows = await q
            .OrderByDescending(x => x.s.SessionDate)
            .ThenByDescending(x => x.s.SessionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new GameSessionAdminListItemDto(
                x.s.SessionId,
                x.s.SessionPublicId,
                x.s.GroupName,
                x.s.SessionDate,
                x.s.StartTime,
                x.s.EndTime,
                x.s.Status,
                x.v.VenueName,
                x.s.CreatedByUserId,
                x.p != null ? x.p.Nickname : null,
                x.s.CreatedDate))
            .ToListAsync();

        return (rows, total);
    }

    public async Task<GameSessionAdminDetailDto?> GetByIdAsync(int sessionId)
    {
        var x = await (
            from s in _db.GameSessions.AsNoTracking()
            join v in _db.Venues.AsNoTracking() on s.VenueId equals v.VenueId
            join u in _db.Users.AsNoTracking() on s.CreatedByUserId equals u.UserId
            join p in _db.UserProfiles.AsNoTracking() on u.UserId equals p.UserId into pj
            from p in pj.DefaultIfEmpty()
            where s.SessionId == sessionId
            select new { s, v, p }).FirstOrDefaultAsync();

        if (x == null)
        {
            return null;
        }

        return new GameSessionAdminDetailDto(
            x.s.SessionId,
            x.s.SessionPublicId,
            x.s.GroupName,
            x.s.SessionDate,
            x.s.StartTime,
            x.s.EndTime,
            x.s.Status,
            x.s.Notes,
            x.s.VenueId,
            x.v.VenueName,
            x.s.CreatedByUserId,
            x.p != null ? x.p.Nickname : null,
            x.s.MaxParticipants,
            x.s.CreatedDate,
            x.s.UpdatedDate);
    }

    public async Task<(GameSessionAdminDetailDto? Data, string Error)> UpdateAsync(int sessionId, GameSessionAdminUpdateDto dto)
    {
        var s = await _db.GameSessions.FirstOrDefaultAsync(a => a.SessionId == sessionId);
        if (s == null)
        {
            return (null, "ไม่พบก๊วน");
        }

        if (dto.Status.HasValue)
        {
            s.Status = dto.Status;
        }

        if (dto.Notes != null)
        {
            s.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        }

        if (dto.GroupName != null)
        {
            var gn = dto.GroupName.Trim();
            if (string.IsNullOrEmpty(gn))
            {
                return (null, "ชื่อกลุ่มไม่ถูกต้อง");
            }

            s.GroupName = gn;
        }

        s.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (await GetByIdAsync(sessionId), string.Empty);
    }

    public async Task<(bool Ok, string Error)> CancelAsync(int sessionId)
    {
        var s = await _db.GameSessions.FirstOrDefaultAsync(a => a.SessionId == sessionId);
        if (s == null)
        {
            return (false, "ไม่พบก๊วน");
        }

        s.Status = 3;
        s.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }
}
