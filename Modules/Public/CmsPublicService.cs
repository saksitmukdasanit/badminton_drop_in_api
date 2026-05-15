using System.Text.Json;
using DropInBadAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Modules.Public;

public interface ICmsPublicService
{
    Task<PublicAppProfileAboutDto> GetAppProfileAboutAsync();
}

public class CmsPublicService : ICmsPublicService
{
    private readonly BadmintonDbContext _db;

    public CmsPublicService(BadmintonDbContext db)
    {
        _db = db;
    }

    public async Task<PublicAppProfileAboutDto> GetAppProfileAboutAsync()
    {
        var about = await _db.CmsAboutSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CmsAboutSettingsId == 1);

        var appLogoUrl = string.Empty;
        var appName = about?.Title ?? string.Empty;
        var appVersion = string.Empty;
        var supportEmail = string.Empty;
        var policyUrl = string.Empty;
        var termsUrl = string.Empty;
        var description = about?.Body ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(about?.Body))
        {
            try
            {
                using var doc = JsonDocument.Parse(about.Body);
                var root = doc.RootElement;
                appLogoUrl = ReadString(root, "appLogoUrl");
                appName = ReadString(root, "appName", appName);
                appVersion = ReadString(root, "appVersion");
                supportEmail = ReadString(root, "supportEmail");
                policyUrl = ReadString(root, "policyUrl");
                termsUrl = ReadString(root, "termsUrl");
                description = ReadString(root, "description", description);
            }
            catch
            {
                // backward compatible with old plain text body
            }
        }

        var policies = await _db.CmsPolicyDocuments.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CmsPolicyDocumentId)
            .ToListAsync();

        var privacy = policies.FirstOrDefault(x =>
            ContainsAny(x.Slug, "privacy", "policy", "pdpa")
            || ContainsAny(x.Title, "นโยบาย", "ความเป็นส่วนตัว", "privacy"));
        var terms = policies.FirstOrDefault(x =>
            ContainsAny(x.Slug, "term", "condition", "tos")
            || ContainsAny(x.Title, "ข้อกำหนด", "เงื่อนไข", "terms"));

        return new PublicAppProfileAboutDto(
            appLogoUrl,
            appName,
            appVersion,
            supportEmail,
            description,
            privacy?.Body ?? string.Empty,
            terms?.Body ?? string.Empty,
            policyUrl,
            termsUrl,
            about?.UpdatedAtUtc ?? DateTime.UtcNow);
    }

    private static string ReadString(JsonElement root, string property, string fallback = "")
    {
        return root.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String
            ? (p.GetString() ?? fallback)
            : fallback;
    }

    private static bool ContainsAny(string? source, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }
        var s = source.ToLowerInvariant();
        return terms.Any(t => s.Contains(t.ToLowerInvariant()));
    }
}
