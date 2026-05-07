using System.Globalization;
using System.Text.Json;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Service.Mobile.Game;

public interface IRecurringGameTemplateService
{
    Task<List<OrganizerRecurringTemplateListDto>> ListAsync(int userId, CancellationToken ct = default);
    Task<OrganizerRecurringTemplateDetailDto?> GetDetailAsync(int templateId, int userId, CancellationToken ct = default);
    Task<OrganizerRecurringTemplateDetailDto> CreateAsync(int userId, SaveOrganizerRecurringTemplateDto dto, CancellationToken ct = default);
    Task<OrganizerRecurringTemplateDetailDto?> UpdateAsync(int templateId, int userId, SaveOrganizerRecurringTemplateDto dto, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(int templateId, int userId, CancellationToken ct = default);
    Task<bool> SetActiveAsync(int templateId, int userId, bool isActive, CancellationToken ct = default);

    /// <summary>สร้างก๊วนที่ยังไม่มีในช่วง ~14 วันข้างหน้า (ทุก template ที่เปิดอยู่ หรือเฉพาะ user)</summary>
    Task<int> GenerateMissingSessionsAsync(int? restrictToUserId, CancellationToken ct = default);
}

public class RecurringGameTemplateService : IRecurringGameTemplateService
{
    private readonly BadmintonDbContext _context;
    private readonly IGameSessionService _gameSessionService;
    private readonly ILogger<RecurringGameTemplateService> _logger;

    public RecurringGameTemplateService(
        BadmintonDbContext context,
        IGameSessionService gameSessionService,
        ILogger<RecurringGameTemplateService> logger)
    {
        _context = context;
        _gameSessionService = gameSessionService;
        _logger = logger;
    }

    public async Task<List<OrganizerRecurringTemplateListDto>> ListAsync(int userId, CancellationToken ct = default)
    {
        var rows = await _context.OrganizerRecurringGameTemplates.AsNoTracking()
            .Where(t => t.CreatedByUserId == userId)
            .OrderByDescending(t => t.UpdatedDate ?? t.CreatedDate)
            .Select(t => new
            {
                t.RecurringTemplateId,
                t.GroupName,
                VenueName = t.VenueNameSnapshot,
                t.DaysOfWeekMask,
                t.IsActive,
                StartTime = RecurringScheduling.FormatTime(t.StartTime),
                EndTime = RecurringScheduling.FormatTime(t.EndTime),
                t.MaxParticipants,
                t.CourtFeePerPerson,
                t.ShuttlecockFeePerPerson,
                t.FacilityIdsCsv,
                GameTypeName = t.GameTypeId != null
                    ? _context.GameTypes.Where(g => g.GameTypeId == t.GameTypeId).Select(g => g.TypeName)
                        .FirstOrDefault()
                    : null,
                ShuttlecockBrandName = t.ShuttlecockModelId != null
                    ? _context.ShuttlecockModels.Where(m => m.ModelId == t.ShuttlecockModelId)
                        .Select(m => m.Brand.BrandName).FirstOrDefault()
                    : null,
                ShuttlecockModelName = t.ShuttlecockModelId != null
                    ? _context.ShuttlecockModels.Where(m => m.ModelId == t.ShuttlecockModelId)
                        .Select(m => m.ModelName).FirstOrDefault()
                    : null,
            })
            .ToListAsync(ct);

        var allFacilityIds = rows
            .SelectMany(r => DeserializeFacilities(r.FacilityIdsCsv))
            .Distinct()
            .ToList();
        var facilityNameById = allFacilityIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Facilities.AsNoTracking()
                .Where(f => allFacilityIds.Contains(f.FacilityId))
                .ToDictionaryAsync(f => f.FacilityId, f => f.FacilityName, ct);

        return rows.ConvertAll(r => new OrganizerRecurringTemplateListDto
        {
            RecurringTemplateId = r.RecurringTemplateId,
            GroupName = r.GroupName,
            VenueName = r.VenueName,
            DaysOfWeekMask = r.DaysOfWeekMask,
            IsActive = r.IsActive,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            MaxParticipants = r.MaxParticipants,
            CourtFeePerPerson = r.CourtFeePerPerson,
            ShuttlecockFeePerPerson = r.ShuttlecockFeePerPerson,
            GameTypeName = r.GameTypeName,
            ShuttlecockBrandName = r.ShuttlecockBrandName,
            ShuttlecockModelName = r.ShuttlecockModelName,
            FacilityNames = FormatFacilityNames(r.FacilityIdsCsv, facilityNameById),
        });
    }

    private static string? FormatFacilityNames(string? facilityIdsCsv, IReadOnlyDictionary<int, string> facilityNameById)
    {
        var ids = DeserializeFacilities(facilityIdsCsv);
        if (ids.Count == 0) return null;
        var names = ids
            .Select(id => facilityNameById.TryGetValue(id, out var n) ? n : null)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    public async Task<OrganizerRecurringTemplateDetailDto?> GetDetailAsync(int templateId, int userId, CancellationToken ct = default)
    {
        var t = await _context.OrganizerRecurringGameTemplates.AsNoTracking()
            .FirstOrDefaultAsync(e => e.RecurringTemplateId == templateId && e.CreatedByUserId == userId, ct);
        return t == null ? null : ToDetailDto(t);
    }

    public async Task<OrganizerRecurringTemplateDetailDto> CreateAsync(int userId, SaveOrganizerRecurringTemplateDto dto, CancellationToken ct = default)
    {
        Validate(dto);

        var entity = new OrganizerRecurringGameTemplate
        {
            CreatedByUserId = userId,
            IsActive = dto.IsActive,
            DaysOfWeekMask = (short)dto.DaysOfWeekMask,
            GroupName = dto.GroupName.Trim(),
            GooglePlaceId = dto.VenueData.GooglePlaceId,
            VenueNameSnapshot = dto.VenueData.Name,
            AddressSnapshot = dto.VenueData.Address,
            Latitude = dto.VenueData.Latitude,
            Longitude = dto.VenueData.Longitude,
            StartTime = RecurringScheduling.ParseRequiredTime(dto.StartTime),
            EndTime = RecurringScheduling.ParseRequiredTime(dto.EndTime),
            GameTypeId = dto.GameTypeId,
            PairingMethodId = dto.PairingMethodId,
            MaxParticipants = dto.MaxParticipants,
            CostingMethod = (short?)dto.CostingMethod,
            CourtFeePerPerson = dto.CourtFeePerPerson,
            ShuttlecockFeePerPerson = dto.ShuttlecockFeePerPerson,
            TotalCourtCost = dto.TotalCourtCost,
            ShuttlecockCostPerUnit = dto.ShuttlecockCostPerUnit,
            ShuttlecockModelId = dto.ShuttlecockModelId,
            NumberOfCourts = dto.NumberOfCourts,
            CourtNumbers = string.IsNullOrWhiteSpace(dto.CourtNumbers) ? null : dto.CourtNumbers.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            FacilityIdsCsv = SerializeFacilities(dto.FacilityIds),
            PhotoUrlsJson = dto.PhotoUrls is { Count: > 0 } ? JsonSerializer.Serialize(dto.PhotoUrls) : null,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };

        _context.OrganizerRecurringGameTemplates.Add(entity);
        await _context.SaveChangesAsync(ct);

        await GenerateMissingSessionsAsync(userId, ct);
        return (await GetDetailAsync(entity.RecurringTemplateId, userId, ct))!;
    }

    public async Task<OrganizerRecurringTemplateDetailDto?> UpdateAsync(int templateId, int userId, SaveOrganizerRecurringTemplateDto dto, CancellationToken ct = default)
    {
        Validate(dto);

        var entity = await _context.OrganizerRecurringGameTemplates
            .FirstOrDefaultAsync(t => t.RecurringTemplateId == templateId && t.CreatedByUserId == userId, ct);

        if (entity == null) return null;

        entity.IsActive = dto.IsActive;
        entity.DaysOfWeekMask = (short)dto.DaysOfWeekMask;
        entity.GroupName = dto.GroupName.Trim();
        entity.GooglePlaceId = dto.VenueData.GooglePlaceId;
        entity.VenueNameSnapshot = dto.VenueData.Name;
        entity.AddressSnapshot = dto.VenueData.Address;
        entity.Latitude = dto.VenueData.Latitude;
        entity.Longitude = dto.VenueData.Longitude;
        entity.StartTime = RecurringScheduling.ParseRequiredTime(dto.StartTime);
        entity.EndTime = RecurringScheduling.ParseRequiredTime(dto.EndTime);
        entity.GameTypeId = dto.GameTypeId;
        entity.PairingMethodId = dto.PairingMethodId;
        entity.MaxParticipants = dto.MaxParticipants;
        entity.CostingMethod = (short?)dto.CostingMethod;
        entity.CourtFeePerPerson = dto.CourtFeePerPerson;
        entity.ShuttlecockFeePerPerson = dto.ShuttlecockFeePerPerson;
        entity.TotalCourtCost = dto.TotalCourtCost;
        entity.ShuttlecockCostPerUnit = dto.ShuttlecockCostPerUnit;
        entity.ShuttlecockModelId = dto.ShuttlecockModelId;
        entity.NumberOfCourts = dto.NumberOfCourts;
        entity.CourtNumbers = string.IsNullOrWhiteSpace(dto.CourtNumbers) ? null : dto.CourtNumbers.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        entity.FacilityIdsCsv = SerializeFacilities(dto.FacilityIds);
        entity.PhotoUrlsJson = dto.PhotoUrls is { Count: > 0 } ? JsonSerializer.Serialize(dto.PhotoUrls) : null;
        entity.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        await GenerateMissingSessionsAsync(userId, ct);
        return await GetDetailAsync(templateId, userId, ct);
    }

    public async Task<bool> SoftDeleteAsync(int templateId, int userId, CancellationToken ct = default)
    {
        var entity = await _context.OrganizerRecurringGameTemplates
            .FirstOrDefaultAsync(t => t.RecurringTemplateId == templateId && t.CreatedByUserId == userId, ct);
        if (entity == null) return false;

        entity.IsActive = false;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetActiveAsync(int templateId, int userId, bool isActive, CancellationToken ct = default)
    {
        var entity = await _context.OrganizerRecurringGameTemplates
            .FirstOrDefaultAsync(t => t.RecurringTemplateId == templateId && t.CreatedByUserId == userId, ct);
        if (entity == null) return false;

        entity.IsActive = isActive;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        if (isActive)
            await GenerateMissingSessionsAsync(userId, ct);

        return true;
    }

    public async Task<int> GenerateMissingSessionsAsync(int? restrictToUserId, CancellationToken ct = default)
    {
        IQueryable<OrganizerRecurringGameTemplate> templatesQuery =
            _context.OrganizerRecurringGameTemplates.AsNoTracking().Where(t => t.IsActive);
        if (restrictToUserId is int uid)
            templatesQuery = templatesQuery.Where(t => t.CreatedByUserId == uid);

        var templates = await templatesQuery.ToListAsync(ct);

        var today = RecurringScheduling.TodayBangkok();
        const int horizonDays = 14;

        var created = 0;
        foreach (var template in templates)
        {
            for (var i = 0; i < horizonDays; i++)
            {
                var date = today.AddDays(i);
                var bit = RecurringScheduling.DayBit(date);
                if (bit == 0 || (template.DaysOfWeekMask & bit) == 0)
                    continue;

                var saveDto = ToSaveGameSessionDto(template, date);
                try
                {
                    // NOTE: ก๊วนที่ถูกสร้างโดยระบบจาก template ไม่ควรยิง noti ไปยัง followers ทีละก๊วน
                    await _gameSessionService.CreateSessionAsync(template.CreatedByUserId, saveDto, notifyFollowers: false);
                    created++;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Recurring skip template {Tid} date {Date}", template.RecurringTemplateId, date);
                }
            }
        }

        if (created > 0)
            _logger.LogInformation("Recurring templates: created {Count} game sessions (restrict user {UserId})", created, restrictToUserId);

        return created;
    }

    private static void Validate(SaveOrganizerRecurringTemplateDto dto)
    {
        if (dto == null) throw new Exception("ข้อมูลไม่ถูกต้อง");
        if (string.IsNullOrWhiteSpace(dto.GroupName)) throw new Exception("กรุณาระบุชื่อก๊วน");
        if (dto.VenueData == null || string.IsNullOrWhiteSpace(dto.VenueData.GooglePlaceId))
            throw new Exception("กรุณาเลือกสนาม");
        if (dto.DaysOfWeekMask is < 1 or > 127) throw new Exception("กรุณาเลือกอย่างน้อยหนึ่งวันในสัปดาห์");
        if (dto.MaxParticipants < 1) throw new Exception("จำนวนที่นั่งไม่ถูกต้อง");

        var st = RecurringScheduling.ParseRequiredTime(dto.StartTime);
        var et = RecurringScheduling.ParseRequiredTime(dto.EndTime);
        if (st >= et) throw new Exception("เวลาเริ่มต้นต้องน้อยกว่าเวลาสิ้นสุด");
    }

    private static SaveGameSessionDto ToSaveGameSessionDto(OrganizerRecurringGameTemplate t, DateOnly sessionDate)
    {
        var venue = new VenueDataDto(
            t.GooglePlaceId,
            t.VenueNameSnapshot,
            t.AddressSnapshot,
            t.Latitude,
            t.Longitude);

        return new SaveGameSessionDto(
            t.GroupName,
            venue,
            sessionDate,
            t.StartTime,
            t.EndTime,
            t.GameTypeId,
            t.PairingMethodId,
            t.MaxParticipants,
            t.CostingMethod,
            t.CourtFeePerPerson,
            t.ShuttlecockFeePerPerson,
            t.TotalCourtCost,
            t.ShuttlecockCostPerUnit,
            t.ShuttlecockModelId,
            t.NumberOfCourts,
            t.CourtNumbers,
            t.Notes,
            DeserializeFacilities(t.FacilityIdsCsv),
            DeserializePhotos(t.PhotoUrlsJson));
    }

    private static OrganizerRecurringTemplateDetailDto ToDetailDto(OrganizerRecurringGameTemplate t)
    {
        var venueData = new VenueDataDto(t.GooglePlaceId, t.VenueNameSnapshot, t.AddressSnapshot, t.Latitude, t.Longitude);
        return new OrganizerRecurringTemplateDetailDto
        {
            RecurringTemplateId = t.RecurringTemplateId,
            VenueData = venueData,
            DaysOfWeekMask = t.DaysOfWeekMask,
            StartTime = RecurringScheduling.FormatTime(t.StartTime),
            EndTime = RecurringScheduling.FormatTime(t.EndTime),
            GroupName = t.GroupName,
            GameTypeId = t.GameTypeId,
            PairingMethodId = t.PairingMethodId,
            MaxParticipants = t.MaxParticipants,
            CostingMethod = t.CostingMethod,
            CourtFeePerPerson = t.CourtFeePerPerson,
            ShuttlecockFeePerPerson = t.ShuttlecockFeePerPerson,
            TotalCourtCost = t.TotalCourtCost,
            ShuttlecockCostPerUnit = t.ShuttlecockCostPerUnit,
            ShuttlecockModelId = t.ShuttlecockModelId,
            NumberOfCourts = t.NumberOfCourts,
            CourtNumbers = t.CourtNumbers,
            Notes = t.Notes,
            FacilityIds = DeserializeFacilities(t.FacilityIdsCsv),
            PhotoUrls = DeserializePhotos(t.PhotoUrlsJson),
            IsActive = t.IsActive,
        };
    }

    private static string? SerializeFacilities(List<int> ids)
    {
        var clean = ids?.Where(id => id > 0).Distinct().ToList();
        if (clean == null || clean.Count == 0) return null;
        return string.Join(",", clean);
    }

    private static List<int> DeserializeFacilities(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<int>();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    private static List<string> DeserializePhotos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}

internal static class RecurringScheduling
{
    private static TimeZoneInfo? _bangkok;

    public static TimeZoneInfo BangkokTz()
    {
        if (_bangkok != null) return _bangkok;
        try
        {
            _bangkok = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
        }
        catch
        {
            _bangkok = TimeZoneInfo.CreateCustomTimeZone("Bangkok+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
        }
        return _bangkok;
    }

    public static DateOnly TodayBangkok()
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BangkokTz());
        return DateOnly.FromDateTime(local.Date);
    }

    public static int DayBit(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => 1 << 0,
        DayOfWeek.Tuesday => 1 << 1,
        DayOfWeek.Wednesday => 1 << 2,
        DayOfWeek.Thursday => 1 << 3,
        DayOfWeek.Friday => 1 << 4,
        DayOfWeek.Saturday => 1 << 5,
        DayOfWeek.Sunday => 1 << 6,
        _ => 0,
    };

    public static TimeOnly ParseRequiredTime(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new Exception("กรุณาระบุเวลา");
        var s = raw.Trim();
        if (TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            return t;
        foreach (var fmt in new[] { @"HH\:mm", @"H\:mm", @"HH\:mm\:ss" })
        {
            if (TimeOnly.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t2))
                return t2;
        }

        throw new Exception("รูปแบบเวลาไม่ถูกต้อง (ใช้ HH:mm เช่น 18:30)");
    }

    public static string FormatTime(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);
}
