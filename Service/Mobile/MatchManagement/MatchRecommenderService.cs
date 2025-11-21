using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Services
{
    public class MatchRecommenderService : IMatchRecommenderService
    {
        private readonly BadmintonDbContext _context;

        public MatchRecommenderService(BadmintonDbContext context)
        {
            _context = context;
        }

        public async Task<List<RecommendedMatchDto>> SuggestMatchesAsync(int sessionId, SuggestionCriteria criteria)
        {
            var waitingPlayers = await GetWaitingPlayersAsync(sessionId);

            if (waitingPlayers.Count < 4)
            {
                return new List<RecommendedMatchDto>();
            }

            switch (criteria)
            {
                case SuggestionCriteria.ByWaitTime:
                    return GenerateMatchesByWaitTime(waitingPlayers);

                case SuggestionCriteria.ByBalancedSkill:
                    return GenerateBalancedSkillMatches(waitingPlayers);

                default:
                    return GenerateMatchesByWaitTime(waitingPlayers);
            }
        }

        private List<RecommendedMatchDto> GenerateMatchesByWaitTime(List<WaitingPlayerDto> players)
        {
            var sortedPlayers = players.OrderBy(p => p.CheckedInTime).ToList();
            var recommendations = new List<RecommendedMatchDto>();

            if (sortedPlayers.Count >= 4)
            {
                var teamA = new List<WaitingPlayerDto> { sortedPlayers[0], sortedPlayers[1] };
                var teamB = new List<WaitingPlayerDto> { sortedPlayers[2], sortedPlayers[3] };

                recommendations.Add(new RecommendedMatchDto
                {
                    TeamA = teamA,
                    TeamB = teamB,
                    MatchBalanceScore = 0,
                    RecommendationReason = "จับคู่ผู้เล่น 4 ท่านที่รอนานที่สุด"
                });
            }

            return recommendations;
        }

        private List<RecommendedMatchDto> GenerateBalancedSkillMatches(List<WaitingPlayerDto> players)
        {
            var playersWithScore = players.Select(p => new
            {
                Player = p,
                Score = GetScoreFromSkillLevel(p.SkillLevelName)
            }).ToList();

            // TODO: Implement a real matchmaking algorithm here.
            return new List<RecommendedMatchDto>();
        }

        private int GetScoreFromSkillLevel(string? skillLevelName)
        {
            if (string.IsNullOrEmpty(skillLevelName)) return 5;
            return skillLevelName.ToUpper() switch
            {
                "S" => 20, "A" => 18, "B+" => 16, "B" => 15, "C+" => 13,
                "C" => 12, "P+" => 10, "P" => 9, "N" => 7, _ => 5
            };
        }

        private async Task<List<WaitingPlayerDto>> GetWaitingPlayersAsync(int sessionId)
        {
            var activeMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 1)
                .SelectMany(m => m.MatchPlayers)
                .ToListAsync();

            var playersInMatchUserIds = activeMatches.Where(mp => mp.UserId.HasValue).Select(mp => mp.UserId).ToHashSet();
            var playersInMatchWalkinIds = activeMatches.Where(mp => mp.WalkinId.HasValue).Select(mp => mp.WalkinId).ToHashSet();

            var waitingMembers = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId && p.CheckinTime != null && p.CheckoutTime == null && !playersInMatchUserIds.Contains(p.UserId))
                .Include(p => p.User.UserProfile).Include(p => p.SkillLevel)
                .Select(p => new WaitingPlayerDto { ParticipantId = p.ParticipantId, ParticipantType = "Member", Nickname = p.User.UserProfile.Nickname, ProfilePhotoUrl = p.User.UserProfile.ProfilePhotoUrl, SkillLevelName = p.SkillLevel != null ? p.SkillLevel.LevelName : null, SkillLevelColor = p.SkillLevel != null ? p.SkillLevel.ColorHexCode : null, CheckedInTime = p.CheckinTime.Value })
                .ToListAsync();

            var waitingGuests = await _context.SessionWalkinGuests
                 .Where(g => g.SessionId == sessionId && g.CheckinTime != null && g.CheckoutTime == null && !playersInMatchWalkinIds.Contains(g.WalkinId))
                 .Include(g => g.SkillLevel)
                 .Select(g => new WaitingPlayerDto { ParticipantId = g.WalkinId, ParticipantType = "Guest", Nickname = g.GuestName, ProfilePhotoUrl = null, SkillLevelName = g.SkillLevel != null ? g.SkillLevel.LevelName : null, SkillLevelColor = g.SkillLevel != null ? g.SkillLevel.ColorHexCode : null, CheckedInTime = g.CheckinTime.Value })
                 .ToListAsync();

            var allWaiting = new List<WaitingPlayerDto>();
            allWaiting.AddRange(waitingMembers);
            allWaiting.AddRange(waitingGuests);

            return allWaiting;
        }
    }
}