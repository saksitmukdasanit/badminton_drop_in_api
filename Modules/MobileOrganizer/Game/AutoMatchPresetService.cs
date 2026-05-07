using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Service.Mobile.Game;

public interface IAutoMatchPresetService
{
    Task<AutoMatchScoringWeightsDto> GetAsync(int organizerUserId);
    Task<AutoMatchScoringWeightsDto> SaveAsync(int organizerUserId, AutoMatchScoringWeightsDto dto);
    Task<AutoMatchScoringWeightsDto> ResetAsync(int organizerUserId);
}

public class AutoMatchPresetService : IAutoMatchPresetService
{
    private readonly BadmintonDbContext _context;

    public AutoMatchPresetService(BadmintonDbContext context)
    {
        _context = context;
    }

    public async Task<AutoMatchScoringWeightsDto> GetAsync(int organizerUserId)
    {
        var preset = await _context.OrganizerAutoMatchPresets
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == organizerUserId);

        if (preset == null) return new AutoMatchScoringWeightsDto();

        return ToDto(preset);
    }

    public async Task<AutoMatchScoringWeightsDto> SaveAsync(int organizerUserId, AutoMatchScoringWeightsDto dto)
    {
        var preset = await _context.OrganizerAutoMatchPresets
            .FirstOrDefaultAsync(p => p.UserId == organizerUserId);

        bool isNew = preset == null;
        preset ??= new OrganizerAutoMatchPreset
        {
            UserId = organizerUserId,
            CreatedDate = DateTime.UtcNow
        };

        // clamp non-negative
        preset.QueuePositionMultiplier = Math.Max(0, dto.QueuePositionMultiplier);
        preset.MatchTogetherPenaltyPerOccurrence = Math.Max(0, dto.MatchTogetherPenaltyPerOccurrence);
        preset.MixedModeOppositeSkillMultiplier = Math.Max(0, dto.MixedModeOppositeSkillMultiplier);
        preset.MixedModeTeammateSkillMultiplier = Math.Max(0, dto.MixedModeTeammateSkillMultiplier);
        preset.SameLevelSkillMultiplier = Math.Max(0, dto.SameLevelSkillMultiplier);
        preset.TeamFormationTeammateHistoryMultiplier = Math.Max(0, dto.TeamFormationTeammateHistoryMultiplier);
        preset.TeamFormationOpponentHistoryMultiplier = Math.Max(0, dto.TeamFormationOpponentHistoryMultiplier);
        preset.UpdatedDate = DateTime.UtcNow;

        if (isNew) _context.OrganizerAutoMatchPresets.Add(preset);
        await _context.SaveChangesAsync();

        return ToDto(preset);
    }

    public async Task<AutoMatchScoringWeightsDto> ResetAsync(int organizerUserId)
    {
        var preset = await _context.OrganizerAutoMatchPresets
            .FirstOrDefaultAsync(p => p.UserId == organizerUserId);

        if (preset != null)
        {
            _context.OrganizerAutoMatchPresets.Remove(preset);
            await _context.SaveChangesAsync();
        }

        return new AutoMatchScoringWeightsDto();
    }

    private static AutoMatchScoringWeightsDto ToDto(OrganizerAutoMatchPreset preset) => new()
    {
        QueuePositionMultiplier = preset.QueuePositionMultiplier,
        MatchTogetherPenaltyPerOccurrence = preset.MatchTogetherPenaltyPerOccurrence,
        MixedModeOppositeSkillMultiplier = preset.MixedModeOppositeSkillMultiplier,
        MixedModeTeammateSkillMultiplier = preset.MixedModeTeammateSkillMultiplier,
        SameLevelSkillMultiplier = preset.SameLevelSkillMultiplier,
        TeamFormationTeammateHistoryMultiplier = preset.TeamFormationTeammateHistoryMultiplier,
        TeamFormationOpponentHistoryMultiplier = preset.TeamFormationOpponentHistoryMultiplier,
    };
}
