using DropInBadAPI.Data;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Admin;

public interface IAdminAboutSettingsService
{
    Task<CmsAboutSettingsDto> GetAsync();
    Task<CmsAboutSettingsDto> SaveAsync(CmsAboutSettingsUpdateDto dto);
}

public class AdminAboutSettingsService : IAdminAboutSettingsService
{
    private const short SingletonId = 1;
    private readonly BadmintonDbContext _db;

    public AdminAboutSettingsService(BadmintonDbContext db)
    {
        _db = db;
    }

    public async Task<CmsAboutSettingsDto> GetAsync()
    {
        var row = await _db.CmsAboutSettings.AsNoTracking().FirstOrDefaultAsync(x => x.CmsAboutSettingsId == SingletonId);
        if (row == null)
        {
            return new CmsAboutSettingsDto(string.Empty, string.Empty, DateTime.UtcNow);
        }

        return new CmsAboutSettingsDto(row.Title, row.Body, row.UpdatedAtUtc);
    }

    public async Task<CmsAboutSettingsDto> SaveAsync(CmsAboutSettingsUpdateDto dto)
    {
        var row = await _db.CmsAboutSettings.FirstOrDefaultAsync(x => x.CmsAboutSettingsId == SingletonId);
        var now = DateTime.UtcNow;
        if (row == null)
        {
            row = new CmsAboutSettings
            {
                CmsAboutSettingsId = SingletonId,
                Title = dto.Title ?? string.Empty,
                Body = dto.Body ?? string.Empty,
                UpdatedAtUtc = now
            };
            _db.CmsAboutSettings.Add(row);
        }
        else
        {
            row.Title = dto.Title ?? string.Empty;
            row.Body = dto.Body ?? string.Empty;
            row.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync();
        return new CmsAboutSettingsDto(row.Title, row.Body, row.UpdatedAtUtc);
    }
}

