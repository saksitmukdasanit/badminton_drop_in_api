using DropInBadAPI.Data;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public interface IAdminAppUsersService
{
    Task<(List<AppUserListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize);
    Task<AppUserDetailDto?> GetByIdAsync(int userId);
    Task<(AppUserDetailDto? Data, string Error)> CreateAsync(AppUserCreateDto dto);
    Task<(AppUserDetailDto? Data, string Error)> UpdateAsync(int userId, AppUserUpdateDto dto);
    Task<(bool Ok, string Error)> SoftDeleteAsync(int userId);
    Task<(List<AdminWalletTransactionDto> Items, long Total)> GetWalletTransactionsAsync(
        int userId,
        short? transactionType,
        string? refQuery,
        string? recipientQuery,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize);
}

public class AdminAppUsersService : IAdminAppUsersService
{
    private readonly BadmintonDbContext _db;

    public AdminAppUsersService(BadmintonDbContext db)
    {
        _db = db;
    }

    public async Task<(List<AppUserListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // แสดงเฉพาะสมาชิกที่มีโปรไฟล์จริง เพื่อให้จำนวนตรงกับตาราง UserProfiles
        var q =
            from p in _db.UserProfiles.AsNoTracking()
            join u in _db.Users.AsNoTracking() on p.UserId equals u.UserId
            where u.DeletedAt == null
            select new { u, p };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.p.Nickname ?? "", pattern)
                || EF.Functions.ILike(x.p.FirstName ?? "", pattern)
                || EF.Functions.ILike(x.p.LastName ?? "", pattern)
                || EF.Functions.ILike(x.p.PhoneNumber ?? "", pattern)
                || EF.Functions.ILike(x.p.PrimaryContactEmail ?? "", pattern)
                || _db.UserLogins.AsNoTracking().Any(l => l.UserId == x.u.UserId && EF.Functions.ILike(l.ProviderEmail ?? "", pattern)));
        }

        var total = await q.LongCountAsync();
        var rows = await q
            .OrderByDescending(x => x.u.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AppUserListItemDto(
                x.u.UserId,
                x.u.UserPublicId,
                x.u.IsActive,
                x.u.CreatedDate,
                x.p.Nickname,
                x.p.PhoneNumber,
                !string.IsNullOrWhiteSpace(x.p.PrimaryContactEmail)
                    ? x.p.PrimaryContactEmail
                    : _db.UserLogins.AsNoTracking()
                        .Where(l => l.UserId == x.u.UserId)
                        .Select(l => l.ProviderEmail)
                        .FirstOrDefault(),
                x.p.FirstName,
                x.p.LastName))
            .ToListAsync();

        return (rows, total);
    }

    public async Task<(AppUserDetailDto? Data, string Error)> CreateAsync(AppUserCreateDto dto)
    {
        var now = DateTime.UtcNow;
        var u = new User
        {
            UserPublicId = Guid.NewGuid(),
            IsActive = true,
            CreatedDate = now
        };
        _db.Users.Add(u);
        await _db.SaveChangesAsync();

        var p = new UserProfile
        {
            UserId = u.UserId,
            Nickname = string.IsNullOrWhiteSpace(dto.Nickname) ? null : dto.Nickname.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
            PrimaryContactEmail = string.IsNullOrWhiteSpace(dto.PrimaryContactEmail) ? null : dto.PrimaryContactEmail.Trim(),
            CreatedDate = now,
            IsPhoneNumberVerified = false
        };
        _db.UserProfiles.Add(p);
        await _db.SaveChangesAsync();

        return (await GetByIdAsync(u.UserId), string.Empty);
    }

    public async Task<AppUserDetailDto?> GetByIdAsync(int userId)
    {
        var x = await (
            from u in _db.Users.AsNoTracking()
            join p in _db.UserProfiles.AsNoTracking() on u.UserId equals p.UserId into pj
            from p in pj.DefaultIfEmpty()
            where u.UserId == userId
            select new { u, p }).FirstOrDefaultAsync();

        if (x == null)
        {
            return null;
        }

        var fallbackProviderEmail = await _db.UserLogins.AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => l.ProviderEmail)
            .FirstOrDefaultAsync();
        var wallet = await BuildWalletSummaryAsync(userId);

        return new AppUserDetailDto(
            x.u.UserId,
            x.u.UserPublicId,
            x.u.IsActive,
            x.u.CreatedDate,
            x.u.DeletedAt,
            x.p?.Nickname,
            x.p?.PhoneNumber,
            string.IsNullOrWhiteSpace(x.p?.PrimaryContactEmail) ? fallbackProviderEmail : x.p!.PrimaryContactEmail,
            x.p?.FirstName,
            x.p?.LastName,
            x.p?.EmergencyContactName,
            x.p?.EmergencyContactPhone,
            x.p?.Gender,
            x.p?.ProfilePhotoUrl,
            x.p?.IsPhoneNumberVerified ?? false,
            wallet);
    }

    public async Task<(AppUserDetailDto? Data, string Error)> UpdateAsync(int userId, AppUserUpdateDto dto)
    {
        var u = await _db.Users.FirstOrDefaultAsync(a => a.UserId == userId);
        if (u == null)
        {
            return (null, "ไม่พบผู้ใช้");
        }

        u.IsActive = dto.IsActive;
        u.UpdatedDate = DateTime.UtcNow;

        var p = await _db.UserProfiles.FirstOrDefaultAsync(a => a.UserId == userId);
        if (p == null)
        {
            p = new UserProfile
            {
                UserId = userId,
                CreatedDate = DateTime.UtcNow,
                IsPhoneNumberVerified = false
            };
            _db.UserProfiles.Add(p);
        }

        p.Nickname = string.IsNullOrWhiteSpace(dto.Nickname) ? null : dto.Nickname.Trim();
        p.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
        p.PrimaryContactEmail = string.IsNullOrWhiteSpace(dto.PrimaryContactEmail) ? null : dto.PrimaryContactEmail.Trim();
        p.FirstName = string.IsNullOrWhiteSpace(dto.FirstName) ? null : dto.FirstName.Trim();
        p.LastName = string.IsNullOrWhiteSpace(dto.LastName) ? null : dto.LastName.Trim();
        p.EmergencyContactName = string.IsNullOrWhiteSpace(dto.EmergencyContactName) ? null : dto.EmergencyContactName.Trim();
        p.EmergencyContactPhone = string.IsNullOrWhiteSpace(dto.EmergencyContactPhone) ? null : dto.EmergencyContactPhone.Trim();
        p.Gender = dto.Gender;
        p.ProfilePhotoUrl = string.IsNullOrWhiteSpace(dto.ProfilePhotoUrl) ? null : dto.ProfilePhotoUrl.Trim();
        p.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var detail = await GetByIdAsync(userId);
        return (detail, string.Empty);
    }

    public async Task<(List<AdminWalletTransactionDto> Items, long Total)> GetWalletTransactionsAsync(
        int userId,
        short? transactionType,
        string? refQuery,
        string? recipientQuery,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var wallet = await _db.UserWallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null)
        {
            return (new List<AdminWalletTransactionDto>(), 0);
        }

        var q = _db.WalletTransactions.AsNoTracking().Where(t => t.WalletId == wallet.WalletId);
        if (transactionType.HasValue)
        {
            q = q.Where(t => t.TransactionType == transactionType.Value);
        }
        if (!string.IsNullOrWhiteSpace(refQuery))
        {
            var rq = refQuery.Trim();
            q = q.Where(t => t.ReferenceId != null && EF.Functions.ILike(t.ReferenceId.ToString()!, $"%{rq}%"));
        }
        if (!string.IsNullOrWhiteSpace(recipientQuery))
        {
            var pattern = $"%{recipientQuery.Trim()}%";
            q = q.Where(t => EF.Functions.ILike(t.Description ?? "", pattern));
        }
        if (fromDate.HasValue)
        {
            q = q.Where(t => t.CreatedDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            q = q.Where(t => t.CreatedDate <= toDate.Value);
        }

        var total = await q.LongCountAsync();
        var rows = await q
            .OrderByDescending(t => t.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new AdminWalletTransactionDto(
                t.TransactionId,
                t.Amount,
                t.TransactionType,
                t.TransactionType == 1 ? "เงินเข้า" : "เงินออก/ถอน",
                ExtractRecipientName(t.Description),
                t.Description,
                t.ReferenceId,
                t.CreatedDate))
            .ToListAsync();

        return (rows, total);
    }

    private async Task<AdminWalletSummaryDto> BuildWalletSummaryAsync(int userId)
    {
        var wallet = await _db.UserWallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null)
        {
            return new AdminWalletSummaryDto(null, 0, 0, 0, 0, 0, 0, new List<AdminWalletTransactionDto>());
        }

        var txs = await _db.WalletTransactions.AsNoTracking()
            .Where(t => t.WalletId == wallet.WalletId)
            .OrderByDescending(t => t.CreatedDate)
            .ToListAsync();

        var totalIn = txs.Where(t => t.TransactionType == 1).Sum(t => t.Amount);
        var totalOut = txs.Where(t => t.TransactionType == 2).Sum(t => t.Amount);
        var recent = txs.Take(10).Select(t => new AdminWalletTransactionDto(
            t.TransactionId,
            t.Amount,
            t.TransactionType,
            t.TransactionType == 1 ? "เงินเข้า" : "เงินออก/ถอน",
            ExtractRecipientName(t.Description),
            t.Description,
            t.ReferenceId,
            t.CreatedDate
        )).ToList();
        var now = DateTime.UtcNow;
        var payoutTxs = txs.Where(t => t.TransactionType == 2).ToList();
        var payout30 = payoutTxs.Where(t => t.CreatedDate >= now.AddDays(-30)).Sum(t => t.Amount);

        return new AdminWalletSummaryDto(
            wallet.WalletId,
            wallet.Balance,
            totalIn,
            totalOut,
            txs.Count,
            payoutTxs.Count,
            payout30,
            recent
        );
    }

    private static string ExtractRecipientName(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "-";
        }
        var d = description.Trim();
        const string prefix = "ถอนเงินเข้าบัญชี ";
        if (d.StartsWith(prefix))
        {
            var remain = d.Substring(prefix.Length);
            var idx = remain.IndexOf(" (", StringComparison.Ordinal);
            return idx > 0 ? remain[..idx].Trim() : remain;
        }
        return d.Length > 64 ? d[..64] : d;
    }

    public async Task<(bool Ok, string Error)> SoftDeleteAsync(int userId)
    {
        var u = await _db.Users.FirstOrDefaultAsync(a => a.UserId == userId);
        if (u == null)
        {
            return (false, "ไม่พบผู้ใช้");
        }

        u.IsActive = false;
        u.DeletedAt = DateTime.UtcNow;
        u.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }
}
