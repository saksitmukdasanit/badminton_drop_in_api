using DropInBadAPI.Data;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public interface IAdminOrganizersService
{
    Task<(List<OrganizerListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize);
    Task<OrganizerDetailDto?> GetByUserIdAsync(int userId);
    Task<(OrganizerDetailDto? Data, string Error)> CreateAsync(OrganizerCreateDto dto);
    Task<(OrganizerDetailDto? Data, string Error)> UpdateAsync(int userId, OrganizerUpdateDto dto);
    Task<(bool Ok, string Error)> SuspendAsync(int userId);
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

public class AdminOrganizersService : IAdminOrganizersService
{
    private readonly BadmintonDbContext _db;

    public AdminOrganizersService(BadmintonDbContext db)
    {
        _db = db;
    }

    public async Task<(List<OrganizerListItemDto> Items, long Total)> ListPagedAsync(string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q =
            from o in _db.OrganizerProfiles.AsNoTracking()
            join u in _db.Users.AsNoTracking() on o.UserId equals u.UserId
            join p in _db.UserProfiles.AsNoTracking() on u.UserId equals p.UserId into pj
            from p in pj.DefaultIfEmpty()
            where u.DeletedAt == null
            select new { o, p };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                (x.p != null && EF.Functions.ILike(x.p.Nickname ?? "", pattern))
                || (x.p != null && EF.Functions.ILike(x.p.PhoneNumber ?? "", pattern))
                || EF.Functions.ILike(x.o.PublicPhoneNumber ?? "", pattern)
                || EF.Functions.ILike(x.o.BankAccountNumber, pattern));
        }

        var total = await q.LongCountAsync();
        var rows = await q
            .OrderByDescending(x => x.o.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OrganizerListItemDto(
                x.o.UserId,
                x.o.Status,
                x.o.PublicPhoneNumber ?? (x.p != null ? x.p.PhoneNumber : null),
                x.p != null ? x.p.Nickname : null,
                x.o.BankId,
                x.o.BankAccountNumber,
                x.o.CreatedDate))
            .ToListAsync();

        return (rows, total);
    }

    public async Task<OrganizerDetailDto?> GetByUserIdAsync(int userId)
    {
        var x = await (
            from o in _db.OrganizerProfiles.AsNoTracking()
            join u in _db.Users.AsNoTracking() on o.UserId equals u.UserId
            join p in _db.UserProfiles.AsNoTracking() on u.UserId equals p.UserId into pj
            from p in pj.DefaultIfEmpty()
            where o.UserId == userId
            select new { o, p }).FirstOrDefaultAsync();

        if (x == null)
        {
            return null;
        }
        var wallet = await BuildWalletSummaryAsync(userId);

        return new OrganizerDetailDto(
            x.o.UserId,
            x.o.Status,
            x.o.ProfilePhotoUrl,
            x.o.NationalId,
            x.o.BankId,
            x.o.BankAccountNumber,
            x.o.BankAccountPhotoUrl,
            x.o.PublicPhoneNumber,
            x.o.FacebookLink,
            x.o.LineId,
            x.o.PhoneVisibility,
            x.o.FacebookVisibility,
            x.o.LineVisibility,
            x.o.XenditAccountId,
            x.p?.Nickname,
            x.p?.PhoneNumber,
            x.p?.PrimaryContactEmail,
            wallet);
    }

    public async Task<(OrganizerDetailDto? Data, string Error)> CreateAsync(OrganizerCreateDto dto)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == dto.UserId && u.DeletedAt == null);
        if (user == null)
        {
            return (null, "ไม่พบผู้ใช้");
        }

        if (await _db.OrganizerProfiles.AnyAsync(o => o.UserId == dto.UserId))
        {
            return (null, "ผู้ใช้นี้เป็นผู้จัดอยู่แล้ว");
        }

        if (!await _db.Banks.AnyAsync(b => b.BankId == dto.BankId))
        {
            return (null, "ไม่พบธนาคาร");
        }

        var acc = dto.BankAccountNumber.Trim();
        if (string.IsNullOrEmpty(acc))
        {
            return (null, "เลขบัญชีจำเป็น");
        }

        var now = DateTime.UtcNow;
        var entity = new OrganizerProfile
        {
            UserId = dto.UserId,
            BankId = dto.BankId,
            BankAccountNumber = acc,
            BankAccountPhotoUrl = string.IsNullOrWhiteSpace(dto.BankAccountPhotoUrl) ? null : dto.BankAccountPhotoUrl.Trim(),
            PublicPhoneNumber = string.IsNullOrWhiteSpace(dto.PublicPhoneNumber) ? null : dto.PublicPhoneNumber.Trim(),
            ProfilePhotoUrl = string.IsNullOrWhiteSpace(dto.ProfilePhotoUrl) ? null : dto.ProfilePhotoUrl.Trim(),
            Status = dto.Status,
            CreatedDate = now,
            PhoneVisibility = 0,
            FacebookVisibility = 0,
            LineVisibility = 0
        };
        _db.OrganizerProfiles.Add(entity);
        await _db.SaveChangesAsync();

        return (await GetByUserIdAsync(dto.UserId), string.Empty);
    }

    public async Task<(OrganizerDetailDto? Data, string Error)> UpdateAsync(int userId, OrganizerUpdateDto dto)
    {
        var o = await _db.OrganizerProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (o == null)
        {
            return (null, "ไม่พบผู้จัด");
        }

        if (!await _db.Banks.AnyAsync(b => b.BankId == dto.BankId))
        {
            return (null, "ไม่พบธนาคาร");
        }

        var acc = dto.BankAccountNumber.Trim();
        if (string.IsNullOrEmpty(acc))
        {
            return (null, "เลขบัญชีจำเป็น");
        }

        o.BankId = dto.BankId;
        o.BankAccountNumber = acc;
        o.BankAccountPhotoUrl = string.IsNullOrWhiteSpace(dto.BankAccountPhotoUrl) ? null : dto.BankAccountPhotoUrl.Trim();
        o.PublicPhoneNumber = string.IsNullOrWhiteSpace(dto.PublicPhoneNumber) ? null : dto.PublicPhoneNumber.Trim();
        o.ProfilePhotoUrl = string.IsNullOrWhiteSpace(dto.ProfilePhotoUrl) ? null : dto.ProfilePhotoUrl.Trim();
        o.FacebookLink = string.IsNullOrWhiteSpace(dto.FacebookLink) ? null : dto.FacebookLink.Trim();
        o.LineId = string.IsNullOrWhiteSpace(dto.LineId) ? null : dto.LineId.Trim();
        o.PhoneVisibility = dto.PhoneVisibility;
        o.FacebookVisibility = dto.FacebookVisibility;
        o.LineVisibility = dto.LineVisibility;
        o.Status = dto.Status;
        o.NationalId = string.IsNullOrWhiteSpace(dto.NationalId) ? null : dto.NationalId.Trim();
        o.XenditAccountId = string.IsNullOrWhiteSpace(dto.XenditAccountId) ? null : dto.XenditAccountId.Trim();
        o.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (await GetByUserIdAsync(userId), string.Empty);
    }

    public async Task<(bool Ok, string Error)> SuspendAsync(int userId)
    {
        var o = await _db.OrganizerProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (o == null)
        {
            return (false, "ไม่พบผู้จัด");
        }

        o.Status = 0;
        o.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, string.Empty);
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
}
