using DropInBadAPI.Constants;
using DropInBadAPI.Dtos;
using DropInBadAPI.Models;

namespace DropInBadAPI.Service.Mobile.Game;

internal static class ParticipantDtoMapper
{
    private const string SkillLevelFallback = "ยังไม่กำหนด";
    private const string SkillColorFallback = "#9E9E9E";

    private static string? GenderName(short? gender) => gender switch
    {
        1 => "ชาย",
        2 => "หญิง",
        3 => "อื่นๆ",
        _ => null
    };

    internal static ParticipantDto FromMember(SessionParticipant p, int gamesPlayed = 0)
    {
        var profile = p.User?.UserProfile;
        return new ParticipantDto
        {
            ParticipantId = p.ParticipantId,
            ParticipantType = ParticipantTypes.Member,
            UserId = p.UserId,
            Nickname = profile?.Nickname,
            FullName = profile == null ? null : $"{profile.FirstName} {profile.LastName}".Trim(),
            GenderName = GenderName(profile?.Gender),
            ProfilePhotoUrl = profile?.ProfilePhotoUrl,
            SkillLevelId = p.SkillLevelId,
            SkillLevelName = p.SkillLevel?.LevelName ?? SkillLevelFallback,
            SkillLevelColor = p.SkillLevel?.ColorHexCode ?? SkillColorFallback,
            Status = p.Status ?? 1,
            CheckinTime = p.CheckinTime,
            CheckoutTime = p.CheckoutTime,
            TotalGamesPlayed = gamesPlayed
        };
    }

    internal static ParticipantDto FromGuest(SessionWalkinGuest g, int gamesPlayed = 0)
    {
        return new ParticipantDto
        {
            ParticipantId = g.WalkinId,
            ParticipantType = ParticipantTypes.Guest,
            UserId = null,
            Nickname = g.GuestName,
            FullName = null,
            GenderName = GenderName(g.Gender),
            ProfilePhotoUrl = null,
            SkillLevelId = g.SkillLevelId,
            SkillLevelName = g.SkillLevel?.LevelName ?? SkillLevelFallback,
            SkillLevelColor = g.SkillLevel?.ColorHexCode ?? SkillColorFallback,
            Status = g.Status ?? 1,
            CheckinTime = g.CheckinTime,
            CheckoutTime = g.CheckoutTime,
            TotalGamesPlayed = gamesPlayed
        };
    }
}
