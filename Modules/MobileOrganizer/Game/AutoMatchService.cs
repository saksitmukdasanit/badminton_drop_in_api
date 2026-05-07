using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Hubs;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Service.Mobile.Game;

public class AutoMatchService : IAutoMatchService
{
    private readonly BadmintonDbContext _context;
    private readonly IHubContext<ManagementGameHub> _hubContext;
    private readonly IMatchManagementService _matchManagementService;

    public AutoMatchService(
        BadmintonDbContext context,
        IHubContext<ManagementGameHub> hubContext,
        IMatchManagementService matchManagementService)
    {
        _context = context;
        _hubContext = hubContext;
        _matchManagementService = matchManagementService;
    }

    public async Task<(bool Success, string ErrorMessage)> AutoMatchAsync(int sessionId, int organizerUserId, AutoMatchRequestDto dto)
    {
        var session = await _context.GameSessions
            .Include(s => s.SessionParticipants).ThenInclude(p => p.User.UserProfile)
            .Include(s => s.SessionParticipants).ThenInclude(p => p.SkillLevel)
            .Include(s => s.SessionWalkinGuests).ThenInclude(g => g.SkillLevel)
            .Include(s => s.Matches).ThenInclude(m => m.MatchPlayers)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

        if (session == null) return (false, "Session not found.");
        if (session.Status != 2) return (false, "Competition has not started yet.");

        var swRaw = dto.ScoringWeights ?? new AutoMatchScoringWeightsDto();
        static int Nz(int v) => Math.Max(0, v);
        int qMul = Nz(swRaw.QueuePositionMultiplier);
        int togetherPen = Nz(swRaw.MatchTogetherPenaltyPerOccurrence);
        int mixedOpp = Nz(swRaw.MixedModeOppositeSkillMultiplier);
        int mixedTm = Nz(swRaw.MixedModeTeammateSkillMultiplier);
        int sameLvl = Nz(swRaw.SameLevelSkillMultiplier);
        int tmMateMul = Nz(swRaw.TeamFormationTeammateHistoryMultiplier);
        int tmOppMul = Nz(swRaw.TeamFormationOpponentHistoryMultiplier);

        var busyUserIds = new HashSet<int>();
        var busyWalkinIds = new HashSet<int>();
        var activeMatches = session.Matches.Where(m => m.Status == 4 || m.Status == 1).ToList();

        foreach (var match in activeMatches)
        {
            foreach (var p in match.MatchPlayers)
            {
                if (p.UserId.HasValue) busyUserIds.Add(p.UserId.Value);
                if (p.WalkinId.HasValue) busyWalkinIds.Add(p.WalkinId.Value);
            }
        }

        var availablePlayers = new List<(int Id, string Type, int? UserId, int? WalkinId, int Skill, int Games, DateTime Wait)>();

        bool IsExcluded(string type, int id)
        {
            var targetId = $"{type}_{id}";
            return dto.ExcludedPlayerIds.Any(ex => string.Equals(ex, targetId, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var p in session.SessionParticipants.Where(p => p.Status == 1))
        {
            if (p.CheckinTime == null || p.CheckoutTime != null) continue;
            if (busyUserIds.Contains(p.UserId) || IsExcluded("Member", p.ParticipantId)) continue;

            var playedMatches = session.Matches.Where(m => m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == p.UserId)).OrderByDescending(m => m.EndTime).ToList();
            int gamesPlayed = playedMatches.Count;
            DateTime waitingSince = playedMatches.FirstOrDefault()?.EndTime ?? p.CheckinTime ?? DateTime.UtcNow;

            int skillRank = p.SkillLevel != null ? (int)p.SkillLevel.LevelRank : 0;
            availablePlayers.Add((p.ParticipantId, "Member", p.UserId, null, skillRank, gamesPlayed, waitingSince));
        }

        foreach (var g in session.SessionWalkinGuests.Where(g => g.Status == 1))
        {
            if (g.CheckinTime == null || g.CheckoutTime != null) continue;
            if (busyWalkinIds.Contains(g.WalkinId) || IsExcluded("Guest", g.WalkinId)) continue;

            var playedMatches = session.Matches.Where(m => m.Status == 2 && m.MatchPlayers.Any(mp => mp.WalkinId == g.WalkinId)).OrderByDescending(m => m.EndTime).ToList();
            int gamesPlayed = playedMatches.Count;
            DateTime waitingSince = playedMatches.FirstOrDefault()?.EndTime ?? g.CheckinTime ?? DateTime.UtcNow;

            int skillRank = g.SkillLevel != null ? (int)g.SkillLevel.LevelRank : 0;
            availablePlayers.Add((g.WalkinId, "Guest", null, g.WalkinId, skillRank, gamesPlayed, waitingSince));
        }

        if (availablePlayers.Count < 4) return (false, "Not enough players available (need 4).");

        var baseSortedPlayers = availablePlayers
            .OrderBy(p => p.Games)
            .ThenBy(p => p.Wait)
            .ToList();

        var selectedPlayers = new List<(int Id, string Type, int? UserId, int? WalkinId, int Skill, int Games, DateTime Wait)>();

        Func<int?, int?, string> getPlayerIdentifier = (userId, walkinId) => userId.HasValue ? $"u_{userId.Value}" : $"w_{walkinId!.Value}";
        Func<string, string, string> getPairKey = (id1, id2) => string.Compare(id1, id2) < 0 ? $"{id1}|{id2}" : $"{id2}|{id1}";

        var matchHistoryGroups = await _context.MatchPlayers
            .Where(mp => mp.Match.SessionId == sessionId && mp.Match.Status == 2)
            .GroupBy(mp => mp.MatchId)
            .Select(g => g.Select(p => new { p.Team, p.UserId, p.WalkinId }).ToList())
            .ToListAsync();

        var teammateHistory = new Dictionary<string, int>();
        var opponentHistory = new Dictionary<string, int>();
        var matchTogetherHistory = new Dictionary<string, int>();

        foreach (var group in matchHistoryGroups)
        {
            for (int i = 0; i < group.Count; i++)
            {
                for (int j = i + 1; j < group.Count; j++)
                {
                    var player1InHistory = group[i];
                    var player2InHistory = group[j];
                    var p1_id = getPlayerIdentifier(player1InHistory.UserId, player1InHistory.WalkinId);
                    var p2_id = getPlayerIdentifier(player2InHistory.UserId, player2InHistory.WalkinId);
                    var pairKey = getPairKey(p1_id, p2_id);

                    matchTogetherHistory[pairKey] = matchTogetherHistory.GetValueOrDefault(pairKey, 0) + 1;

                    if (player1InHistory.Team == player2InHistory.Team)
                    {
                        teammateHistory[pairKey] = teammateHistory.GetValueOrDefault(pairKey, 0) + 1;
                    }
                    else
                    {
                        opponentHistory[pairKey] = opponentHistory.GetValueOrDefault(pairKey, 0) + 1;
                    }
                }
            }
        }

        var firstPlayer = baseSortedPlayers.First();
        selectedPlayers.Add(firstPlayer);

        var remainingPool = baseSortedPlayers.Skip(1).ToList();

        for (int i = 0; i < 3; i++)
        {
            var bestCandidate = remainingPool
                .OrderBy(c =>
                {
                    int queueScore = baseSortedPlayers.IndexOf(c) * qMul;

                    int historyCount = 0;
                    var c_id = getPlayerIdentifier(c.UserId, c.WalkinId);
                    foreach (var s in selectedPlayers)
                    {
                        var s_id = getPlayerIdentifier(s.UserId, s.WalkinId);
                        historyCount += matchTogetherHistory.GetValueOrDefault(getPairKey(c_id, s_id), 0);
                    }
                    int historyScore = historyCount * togetherPen;

                    int skillScore = 0;
                    if (dto.IsMixedMode)
                    {
                        if (selectedPlayers.Count == 1)
                        {
                            skillScore = -Math.Abs(c.Skill - selectedPlayers[0].Skill) * mixedOpp;
                        }
                        else if (selectedPlayers.Count == 2)
                        {
                            skillScore = Math.Abs(c.Skill - selectedPlayers[0].Skill) * mixedTm;
                        }
                        else
                        {
                            skillScore = Math.Abs(c.Skill - selectedPlayers[1].Skill) * mixedTm;
                        }
                    }
                    else
                    {
                        skillScore = Math.Abs(c.Skill - selectedPlayers[0].Skill) * sameLvl;
                    }

                    return queueScore + historyScore + skillScore;
                })
                .First();

            selectedPlayers.Add(bestCandidate);
            remainingPool.Remove(bestCandidate);
        }

        if (selectedPlayers.Count < 4)
        {
            return (false, "Not enough players to form a match with the selected criteria.");
        }

        var teamA = new List<(int Id, string Type, int? UserId, int? WalkinId, int Skill, int Games, DateTime Wait)>();
        var teamB = new List<(int Id, string Type, int? UserId, int? WalkinId, int Skill, int Games, DateTime Wait)>();

        var sortedSelectedPlayers = selectedPlayers.OrderBy(p => p.Skill).ToList();
        var p1 = sortedSelectedPlayers[0]; var p2 = sortedSelectedPlayers[1]; var p3 = sortedSelectedPlayers[2]; var p4 = sortedSelectedPlayers[3];

        var combinations = new List<(List<dynamic> team1, List<dynamic> team2)>
        {
            (new List<dynamic> { p1, p2 }, new List<dynamic> { p3, p4 }),
            (new List<dynamic> { p1, p3 }, new List<dynamic> { p2, p4 }),
            (new List<dynamic> { p1, p4 }, new List<dynamic> { p2, p3 })
        };

        var scoredCombinations = combinations.Select(combo =>
        {
            var typedTeamA = combo.team1.Cast<(int Id, string Type, int? UserId, int? WalkinId, int Skill, int Games, DateTime Wait)>().ToList();
            var typedTeamB = combo.team2.Cast<(int Id, string Type, int? UserId, int? WalkinId, int Skill, int Games, DateTime Wait)>().ToList();

            double balanceScore = Math.Abs(typedTeamA.Sum(pl => pl.Skill) - typedTeamB.Sum(pl => pl.Skill));
            int historyScore = 0;

            var tA_p1_id = getPlayerIdentifier(typedTeamA[0].UserId, typedTeamA[0].WalkinId);
            var tA_p2_id = getPlayerIdentifier(typedTeamA[1].UserId, typedTeamA[1].WalkinId);
            historyScore += teammateHistory.GetValueOrDefault(getPairKey(tA_p1_id, tA_p2_id), 0) * tmMateMul;

            var tB_p1_id = getPlayerIdentifier(typedTeamB[0].UserId, typedTeamB[0].WalkinId);
            var tB_p2_id = getPlayerIdentifier(typedTeamB[1].UserId, typedTeamB[1].WalkinId);
            historyScore += teammateHistory.GetValueOrDefault(getPairKey(tB_p1_id, tB_p2_id), 0) * tmMateMul;

            historyScore += opponentHistory.GetValueOrDefault(getPairKey(tA_p1_id, tB_p1_id), 0) * tmOppMul;
            historyScore += opponentHistory.GetValueOrDefault(getPairKey(tA_p1_id, tB_p2_id), 0) * tmOppMul;
            historyScore += opponentHistory.GetValueOrDefault(getPairKey(tA_p2_id, tB_p1_id), 0) * tmOppMul;
            historyScore += opponentHistory.GetValueOrDefault(getPairKey(tA_p2_id, tB_p2_id), 0) * tmOppMul;

            return new { Combination = combo, BalanceScore = balanceScore, HistoryScore = historyScore };
        }).ToList();

        var bestCombination = scoredCombinations
            .OrderBy(c => c.HistoryScore)
            .ThenBy(c => c.BalanceScore)
            .First();

        teamA = bestCombination.Combination.team1.Cast<(int Id, string Type, int? UserId, int? WalkinId, int Skill, int Games, DateTime Wait)>().ToList();
        teamB = bestCombination.Combination.team2.Cast<(int Id, string Type, int? UserId, int? WalkinId, int Skill, int Games, DateTime Wait)>().ToList();

        var allCourts = !string.IsNullOrEmpty(session.CourtNumbers)
            ? session.CourtNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
            : new List<string>();

        if (!allCourts.Any())
        {
            allCourts = Enumerable.Range(1, session.NumberOfCourts ?? 1).Select(i => i.ToString()).ToList();
        }

        var usedCourts = activeMatches
            .Where(m => m.Status == 1 || m.MatchPlayers.Any())
            .Select(m => m.CourtNumber)
            .ToHashSet();

        string? targetCourt = null;
        foreach (var court in allCourts)
        {
            if (!usedCourts.Contains(court))
            {
                targetCourt = court;
                break;
            }
        }

        if (targetCourt == null)
        {
            int reserveIndex = 1;
            while (usedCourts.Contains($"-{reserveIndex}"))
            {
                reserveIndex++;
            }
            targetCourt = $"-{reserveIndex}";
        }

        Match newMatch;
        var ghostMatch = activeMatches.FirstOrDefault(m => m.CourtNumber == targetCourt && m.Status == 4 && !m.MatchPlayers.Any());

        if (ghostMatch != null)
        {
            newMatch = ghostMatch;
            newMatch.CreatedDate = DateTime.UtcNow;
            newMatch.MatchPlayers.Clear();
        }
        else
        {
            newMatch = new Match
            {
                SessionId = sessionId,
                CourtNumber = targetCourt,
                Status = 4,
                CreatedDate = DateTime.UtcNow,
                ShuttlecocksUsed = 0,
                MatchPlayers = new List<MatchPlayer>()
            };
            _context.Matches.Add(newMatch);
        }

        foreach (var p in teamA)
        {
            newMatch.MatchPlayers.Add(new MatchPlayer
            {
                UserId = p.UserId,
                WalkinId = p.WalkinId,
                Team = "A"
            });
        }
        foreach (var p in teamB)
        {
            newMatch.MatchPlayers.Add(new MatchPlayer
            {
                UserId = p.UserId,
                WalkinId = p.WalkinId,
                Team = "B"
            });
        }

        await _context.SaveChangesAsync();

        await BroadcastLiveStateChange(sessionId, organizerUserId);
        return (true, "Match created successfully.");
    }

    public async Task<(bool Success, string ErrorMessage)> SwapPlayersAsync(int sessionId, int organizerUserId, SwapPlayersRequestDto dto)
    {
        var session = await _context.GameSessions
            .Include(s => s.Matches).ThenInclude(m => m.MatchPlayers)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

        if (session == null) return (false, "Session not found.");

        var stagedMatches = session.Matches.Where(m => m.Status == 4).ToList();

        int? p1UserId = null;
        if (string.Equals(dto.Player1.Type, "Member", StringComparison.OrdinalIgnoreCase))
        {
            var sp = await _context.SessionParticipants.FindAsync(dto.Player1.Id);
            p1UserId = sp?.UserId;
            if (p1UserId == null) return (false, "Player 1 (Member) not found.");
        }

        int? p2UserId = null;
        if (string.Equals(dto.Player2.Type, "Member", StringComparison.OrdinalIgnoreCase))
        {
            var sp = await _context.SessionParticipants.FindAsync(dto.Player2.Id);
            p2UserId = sp?.UserId;
            if (p2UserId == null) return (false, "Player 2 (Member) not found.");
        }

        MatchPlayer? mp1 = null;
        MatchPlayer? mp2 = null;

        foreach (var match in stagedMatches)
        {
            var p1 = match.MatchPlayers.FirstOrDefault(p =>
                (string.Equals(dto.Player1.Type, "Member", StringComparison.OrdinalIgnoreCase) && p.UserId == p1UserId) ||
                (string.Equals(dto.Player1.Type, "Guest", StringComparison.OrdinalIgnoreCase) && p.WalkinId == dto.Player1.Id));

            if (p1 != null) { mp1 = p1; }

            var p2 = match.MatchPlayers.FirstOrDefault(p =>
                (string.Equals(dto.Player2.Type, "Member", StringComparison.OrdinalIgnoreCase) && p.UserId == p2UserId) ||
                (string.Equals(dto.Player2.Type, "Guest", StringComparison.OrdinalIgnoreCase) && p.WalkinId == dto.Player2.Id));

            if (p2 != null) { mp2 = p2; }
        }

        if (mp1 == null || mp2 == null) return (false, "One or both players not found in staged matches.");

        var tempUserId = mp1.UserId;
        var tempWalkinId = mp1.WalkinId;

        mp1.UserId = mp2.UserId;
        mp1.WalkinId = mp2.WalkinId;

        mp2.UserId = tempUserId;
        mp2.WalkinId = tempWalkinId;

        await _context.SaveChangesAsync();
        await BroadcastLiveStateChange(sessionId, organizerUserId);
        return (true, "Players swapped successfully.");
    }

    public async Task<(bool Success, string ErrorMessage)> AssignReserveToCourtAsync(int sessionId, int organizerUserId, AssignReserveRequestDto dto)
    {
        var session = await _context.GameSessions
            .Include(s => s.Matches).ThenInclude(m => m.MatchPlayers)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

        if (session == null) return (false, "Session not found.");

        var targetMatch = session.Matches.FirstOrDefault(m => m.Status == 4 && m.CourtNumber == dto.TargetCourtIdentifier);

        if (targetMatch != null)
        {
            _context.MatchPlayers.RemoveRange(targetMatch.MatchPlayers);
            targetMatch.MatchPlayers.Clear();
        }
        else
        {
            targetMatch = new Match
            {
                SessionId = sessionId,
                CourtNumber = dto.TargetCourtIdentifier,
                Status = 4,
                CreatedDate = DateTime.UtcNow,
                MatchPlayers = new List<MatchPlayer>()
            };
            _context.Matches.Add(targetMatch);
        }

        Match? reserveMatch = null;
        var reserveMatches = session.Matches
            .Where(m => m.Status == 4 && m.CourtNumber != null && m.CourtNumber.StartsWith("-"))
            .ToList();

        if (dto.IsQueueMode)
        {
            reserveMatch = reserveMatches
                .Where(m => m.MatchPlayers.Any())
                .OrderByDescending(m => int.Parse(m.CourtNumber!))
                .FirstOrDefault();
        }
        else
        {
            if (int.TryParse(dto.TargetCourtIdentifier, out int courtNum))
            {
                string targetReserveId = $"-{courtNum}";
                reserveMatch = reserveMatches.FirstOrDefault(m => m.CourtNumber == targetReserveId);
            }
        }

        if (reserveMatch == null || !reserveMatch.MatchPlayers.Any())
        {
            return (false, "No suitable reserve team found.");
        }

        foreach (var p in reserveMatch.MatchPlayers)
        {
            targetMatch.MatchPlayers.Add(new MatchPlayer
            {
                UserId = p.UserId,
                WalkinId = p.WalkinId,
                Team = p.Team
            });
        }

        _context.MatchPlayers.RemoveRange(reserveMatch.MatchPlayers);

        await _context.SaveChangesAsync();
        await BroadcastLiveStateChange(sessionId, organizerUserId);
        return (true, "Reserve team assigned to court successfully.");
    }

    public async Task<(bool Success, string ErrorMessage)> MovePlayersAsync(int sessionId, int organizerUserId, MovePlayersRequestDto dto)
    {
        var session = await _context.GameSessions
            .Include(s => s.Matches).ThenInclude(m => m.MatchPlayers)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

        if (session == null) return (false, "Session not found.");

        var targetMatch = session.Matches.FirstOrDefault(m => m.Status == 4 && m.CourtNumber == dto.TargetCourtIdentifier);
        if (targetMatch == null)
        {
            targetMatch = new Match
            {
                SessionId = sessionId,
                CourtNumber = dto.TargetCourtIdentifier,
                Status = 4,
                CreatedDate = DateTime.UtcNow,
                MatchPlayers = new List<MatchPlayer>()
            };
            _context.Matches.Add(targetMatch);
        }

        foreach (var playerDto in dto.Players)
        {
            int? userId = null;
            int? walkinId = null;
            bool isMember = string.Equals(playerDto.Type, "Member", StringComparison.OrdinalIgnoreCase);

            if (isMember)
            {
                var sp = await _context.SessionParticipants.FindAsync(playerDto.Id);
                userId = sp?.UserId;
                if (userId == null) continue;
            }
            else
            {
                walkinId = playerDto.Id;
            }

            bool alreadyInTarget = targetMatch.MatchPlayers.Any(p =>
                (isMember && p.UserId == userId) ||
                (!isMember && p.WalkinId == walkinId));

            if (alreadyInTarget) continue;

            var existingEntry = session.Matches
                .Where(m => m.Status == 4)
                .SelectMany(m => m.MatchPlayers)
                .FirstOrDefault(p =>
                    (isMember && p.UserId == userId) ||
                    (!isMember && p.WalkinId == walkinId));

            if (existingEntry != null)
            {
                _context.MatchPlayers.Remove(existingEntry);

                var parentMatch = session.Matches.FirstOrDefault(m => m.MatchId == existingEntry.MatchId);
                if (parentMatch != null)
                {
                    parentMatch.MatchPlayers.Remove(existingEntry);
                }
            }

            if (targetMatch.MatchPlayers.Count < 4)
            {
                string team = targetMatch.MatchPlayers.Count < 2 ? "A" : "B";

                targetMatch.MatchPlayers.Add(new MatchPlayer
                {
                    UserId = userId,
                    WalkinId = walkinId,
                    Team = team
                });
            }
        }

        var emptyMatches = session.Matches.Where(m => m.Status == 4 && !m.MatchPlayers.Any() && m.MatchId != targetMatch.MatchId).ToList();
        _context.Matches.RemoveRange(emptyMatches);

        await _context.SaveChangesAsync();
        await BroadcastLiveStateChange(sessionId, organizerUserId);
        return (true, "Players moved successfully.");
    }

    private async Task BroadcastLiveStateChange(int sessionId, int organizerUserId)
    {
        var liveState = await _matchManagementService.GetLiveStateAsync(sessionId, organizerUserId);
        if (liveState != null)
        {
            await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("ReceiveLiveStateUpdate", liveState);
        }
    }
}
