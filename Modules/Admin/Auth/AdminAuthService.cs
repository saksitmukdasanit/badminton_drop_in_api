using DropInBadAPI.Data;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using DropInBadAPI.Modules.Auth;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public class AdminAuthService : IAdminAuthService
{
    private readonly BadmintonDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly IConfiguration _config;

    public AdminAuthService(BadmintonDbContext db, IJwtService jwt, IPasswordHasher hasher, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _hasher = hasher;
        _config = config;
    }

    public async Task<(AdminTokenResponseDto? Data, string Error)> LoginAsync(AdminLoginDto dto, string? ipAddress)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(dto.Password))
        {
            return (null, "อีเมลหรือรหัสผ่านไม่ถูกต้อง");
        }

        // เปรียบเทียบแบบไม่สนตัวพิมพ์ — ข้อมูลที่ insert มืออาจเป็น Admin@... แต่ client ส่งเป็น admin@...
        var admin = await _db.CmsAdminUsers
            .FirstOrDefaultAsync(a => a.Email.ToLower() == email);
        if (admin == null || !admin.IsActive)
        {
            return (null, "อีเมลหรือรหัสผ่านไม่ถูกต้อง");
        }

        var storedHash = admin.PasswordHash?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(storedHash) || !_hasher.Verify(dto.Password, storedHash))
        {
            return (null, "อีเมลหรือรหัสผ่านไม่ถูกต้อง");
        }

        return await IssueTokensAsync(admin, ipAddress, auditLogin: true);
    }

    public async Task<(AdminTokenResponseDto? Data, string Error)> RefreshAsync(AdminRefreshDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            return (null, "Refresh token ไม่ถูกต้อง");
        }

        var admin = await _db.CmsAdminUsers.FirstOrDefaultAsync(a => a.RefreshToken == dto.RefreshToken);
        if (admin == null || !admin.IsActive || admin.RefreshTokenExpiryUtc == null || admin.RefreshTokenExpiryUtc <= DateTime.UtcNow)
        {
            return (null, "Refresh token หมดอายุหรือไม่ถูกต้อง กรุณาเข้าสู่ระบบใหม่");
        }

        return await IssueTokensAsync(admin, ipAddress: null, auditLogin: false);
    }

    public async Task<(bool Ok, string Error)> LogoutAsync(int cmsAdminUserId)
    {
        var admin = await _db.CmsAdminUsers.FindAsync(cmsAdminUserId);
        if (admin == null) return (false, "ไม่พบบัญชี");

        admin.RefreshToken = null;
        admin.RefreshTokenExpiryUtc = null;
        admin.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }

    private async Task<(AdminTokenResponseDto? Data, string Error)> IssueTokensAsync(CmsAdminUser admin, string? ipAddress, bool auditLogin)
    {
        var slidingDays = _config.GetValue("CmsAuth:SlidingRefreshDays", 7);
        var refresh = _jwt.CreateRefreshToken();
        admin.RefreshToken = refresh;
        admin.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(slidingDays);
        admin.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var access = _jwt.CreateAdminAccessToken(admin);
        if (auditLogin)
        {
            _db.AdminAuditLogs.Add(new AdminAuditLog
            {
                CmsAdminUserId = admin.CmsAdminUserId,
                Action = "admin.login",
                EntityType = "CmsAdminUser",
                EntityId = admin.CmsAdminUserId.ToString(),
                IpAddress = ipAddress,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        var data = new AdminTokenResponseDto(
            AccessToken: access,
            RefreshToken: refresh,
            AdminUserId: admin.CmsAdminUserId,
            Email: admin.Email,
            DisplayName: admin.DisplayName);
        return (data, string.Empty);
    }
}
