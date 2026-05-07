using DropInBadAPI.Data;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.UserSafety;

public class UserSafetyService : IUserSafetyService
{
    private static readonly HashSet<string> _allowedReasons = new()
    {
        "spam", "harassment", "fraud", "fake_profile", "inappropriate_content", "other"
    };

    private readonly BadmintonDbContext _context;
    private readonly ILogger<UserSafetyService> _logger;

    public UserSafetyService(BadmintonDbContext context, ILogger<UserSafetyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> ReportUserAsync(int reporterUserId, ReportUserDto dto)
    {
        if (reporterUserId == dto.ReportedUserId)
        {
            return (false, "ไม่สามารถรายงานบัญชีตนเองได้");
        }
        if (!_allowedReasons.Contains(dto.Reason))
        {
            return (false, "เหตุผลไม่ถูกต้อง");
        }

        var reportedExists = await _context.Users.AnyAsync(u => u.UserId == dto.ReportedUserId);
        if (!reportedExists)
        {
            return (false, "ไม่พบผู้ใช้ที่ต้องการรายงาน");
        }

        // ป้องกันการสแปม — ไม่รับรายงานเดิมซ้ำใน 24 ชั่วโมง (เหตุผลเดียวกัน + reporter เดียวกัน)
        var since = DateTime.UtcNow.AddHours(-24);
        var duplicate = await _context.UserReports.AnyAsync(r =>
            r.ReporterUserId == reporterUserId &&
            r.ReportedUserId == dto.ReportedUserId &&
            r.Reason == dto.Reason &&
            r.CreatedAt >= since);
        if (duplicate)
        {
            return (false, "คุณรายงานผู้ใช้นี้ในเหตุผลเดียวกันแล้ว — กรุณารอการตรวจสอบ");
        }

        var report = new UserReport
        {
            ReporterUserId = reporterUserId,
            ReportedUserId = dto.ReportedUserId,
            Reason = dto.Reason,
            Description = dto.Description,
            SessionId = dto.SessionId,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserReports.Add(report);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "User {Reporter} reported user {Reported} for {Reason} (session={SessionId})",
            reporterUserId, dto.ReportedUserId, dto.Reason, dto.SessionId);

        return (true, "ได้รับรายงานของคุณแล้ว ทีมงานจะตรวจสอบโดยเร็วที่สุด");
    }

    public async Task<(bool Success, string Message)> BlockUserAsync(int blockerUserId, int blockedUserId)
    {
        if (blockerUserId == blockedUserId)
        {
            return (false, "ไม่สามารถบล็อกบัญชีตนเองได้");
        }

        var blockedExists = await _context.Users.AnyAsync(u => u.UserId == blockedUserId);
        if (!blockedExists)
        {
            return (false, "ไม่พบผู้ใช้");
        }

        var existing = await _context.UserBlocks.FirstOrDefaultAsync(b =>
            b.BlockerUserId == blockerUserId && b.BlockedUserId == blockedUserId);
        if (existing != null)
        {
            return (true, "ผู้ใช้รายนี้ถูกบล็อกอยู่แล้ว");
        }

        _context.UserBlocks.Add(new UserBlock
        {
            BlockerUserId = blockerUserId,
            BlockedUserId = blockedUserId,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return (true, "บล็อกผู้ใช้เรียบร้อย");
    }

    public async Task<(bool Success, string Message)> UnblockUserAsync(int blockerUserId, int blockedUserId)
    {
        var existing = await _context.UserBlocks.FirstOrDefaultAsync(b =>
            b.BlockerUserId == blockerUserId && b.BlockedUserId == blockedUserId);
        if (existing == null)
        {
            return (false, "ไม่พบรายการบล็อก");
        }

        _context.UserBlocks.Remove(existing);
        await _context.SaveChangesAsync();
        return (true, "ยกเลิกการบล็อกเรียบร้อย");
    }

    public async Task<List<BlockedUserItemDto>> GetBlockedUsersAsync(int blockerUserId)
    {
        return await _context.UserBlocks
            .Where(b => b.BlockerUserId == blockerUserId)
            .Join(_context.UserProfiles,
                b => b.BlockedUserId,
                p => p.UserId,
                (b, p) => new BlockedUserItemDto(p.UserId, p.Nickname, p.ProfilePhotoUrl, b.CreatedAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
}
