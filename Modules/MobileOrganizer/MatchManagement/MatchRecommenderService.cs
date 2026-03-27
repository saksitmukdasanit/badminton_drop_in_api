using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using DropInBadAPI.Utilities;

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

        private async Task<List<WaitingPlayerDto>> GetWaitingPlayersAsync(int sessionId)
        {
            var activeMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 1)
                .Include(m => m.MatchPlayers)
                .ToListAsync();

            var busyUserIds = activeMatches.SelectMany(m => m.MatchPlayers).Where(mp => mp.UserId.HasValue).Select(mp => mp.UserId.Value).ToHashSet();
            var busyWalkinIds = activeMatches.SelectMany(m => m.MatchPlayers).Where(mp => mp.WalkinId.HasValue).Select(mp => mp.WalkinId.Value).ToHashSet();

            var members = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId && p.Status == 1 && p.CheckinTime != null && p.CheckoutTime == null && !busyUserIds.Contains(p.UserId))
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .Select(p => new WaitingPlayerDto
                {
                    ParticipantId = p.ParticipantId,
                    ParticipantType = "Member",
                    Nickname = p.User.UserProfile.Nickname,
                    CheckedInTime = p.CheckinTime.Value,
                    SkillLevelName = p.SkillLevel != null ? p.SkillLevel.LevelName : null
                }).ToListAsync();

            var guests = await _context.SessionWalkinGuests
                .Where(g => g.SessionId == sessionId && g.Status == 1 && g.CheckinTime != null && g.CheckoutTime == null && !busyWalkinIds.Contains(g.WalkinId))
                .Include(g => g.SkillLevel)
                .Select(g => new WaitingPlayerDto
                {
                    ParticipantId = g.WalkinId,
                    ParticipantType = "Guest",
                    Nickname = g.GuestName,
                    CheckedInTime = g.CheckinTime.Value,
                    SkillLevelName = g.SkillLevel != null ? g.SkillLevel.LevelName : null
                }).ToListAsync();

            return members.Concat(guests).ToList();
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
            })
            .OrderBy(p => p.Player.TotalGamesPlayed)
            .ThenBy(p => p.Player.CheckedInTime)
            .ToList();

            var recommendations = new List<RecommendedMatchDto>();
            if (playersWithScore.Count >= 4)
            {
                var first = playersWithScore.First();
                var remainingPlayers = playersWithScore.Skip(1).ToList();
                
                // ใช้ Combinatorics หาตัวเลือก 3 คนจากคิวทั้งหมด ที่เมื่อรวมกับคนแรกแล้วแบ่งทีมได้สูสีที่สุด
                var bestGroup = remainingPlayers.Combinations(3)
                    .Select(combo =>
                    {
                        var groupOfFour = new[] { first }.Concat(combo).OrderBy(p => p.Score).ToList();
                        var teamA_Score = groupOfFour[0].Score + groupOfFour[3].Score; // อ่อนสุด + เก่งสุด
                        var teamB_Score = groupOfFour[1].Score + groupOfFour[2].Score; // กลาง + กลาง
                        var balanceDiff = Math.Abs(teamA_Score - teamB_Score);

                        return new { Group = groupOfFour, BalanceDiff = balanceDiff };
                    })
                    .OrderBy(x => x.BalanceDiff) // เลือกกลุ่มที่ผลต่างคะแนนน้อยที่สุด (สูสีที่สุด)
                    .First();

                recommendations.Add(new RecommendedMatchDto
                {
                    TeamA = new List<WaitingPlayerDto> { bestGroup.Group[0].Player, bestGroup.Group[3].Player },
                    TeamB = new List<WaitingPlayerDto> { bestGroup.Group[1].Player, bestGroup.Group[2].Player },
                    MatchBalanceScore = bestGroup.BalanceDiff,
                    RecommendationReason = "จัดตามระดับมือ (คำนวณจากทุกความเป็นไปได้เพื่อความสมดุลที่สุด)"
                });
            }
            return recommendations;
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
    }
}