using Microsoft.Extensions.Configuration;
using DropInBadAPI.Constants;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Hubs;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Services
{
    public partial class MatchManagementService
    {
        public async Task<LiveSessionStateDto?> GetLiveStateAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId) return null;

            // 1. ดึงข้อมูลแมตช์ที่กำลังเล่นอยู่ทั้งหมดใน Session นี้ (Status = 1)
            var activeMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 1)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User!).ThenInclude(u => u.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User!).ThenInclude(u => u.SessionParticipants.Where(sp => sp.SessionId == sessionId)).ThenInclude(sp => sp.SkillLevel)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin).ThenInclude(w => w!.SkillLevel)
                .ToListAsync();

            // 2. ดึงข้อมูลแมตช์ที่จัดเตรียมไว้ (Status = 4)
            var stagedMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 4)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User!).ThenInclude(u => u.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User!).ThenInclude(u => u.SessionParticipants.Where(sp => sp.SessionId == sessionId)).ThenInclude(sp => sp.SkillLevel)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin).ThenInclude(w => w!.SkillLevel)
                .OrderBy(m => m.CreatedDate)
                .ToListAsync();

            // 3. สร้าง List ของชื่อสนามจริง (Official Courts) ขึ้นมาก่อน เพื่อใช้ตรวจสอบ
            var courtIdentifiers = session.CourtNumbers?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(c => c.Trim())
                                    .ToList();

            if (courtIdentifiers == null || !courtIdentifiers.Any())
            {
                courtIdentifiers = Enumerable.Range(1, session.NumberOfCourts ?? 1).Select(i => i.ToString()).ToList();
            }

            // สร้าง HashSet เพื่อให้ตรวจสอบชื่อสนามได้เร็วและแม่นยำ (Case-insensitive)
            var validCourtSet = new HashSet<string>(courtIdentifiers, StringComparer.OrdinalIgnoreCase);

            // 4. แปลงแมตช์ที่กำลังเล่นอยู่ให้เป็น Dictionary
            var activeMatchesMap = activeMatches.ToDictionary(
                match => match.CourtNumber ?? "", // Handle null key
                match => new CurrentlyPlayingMatchDto
                {
                    MatchId = match.MatchId,
                    CourtNumber = match.CourtNumber,
                    StartTime = match.StartTime,
                    TeamA = match.MatchPlayers.Where(p => p.Team == "A").Select(MapToPlayerInMatchDto).ToList(),
                    TeamB = match.MatchPlayers.Where(p => p.Team == "B").Select(MapToPlayerInMatchDto).ToList()
                });

            // 5. แยก Staged Matches โดยใช้ Logic String Whitelist

            // กลุ่ม A: ลงสนามจริง (CourtNumber มีค่า และ "มีชื่ออยู่ในรายการสนามของ Session")
            var stagedMatchesForCourts = stagedMatches
                .Where(m => !string.IsNullOrEmpty(m.CourtNumber) && validCourtSet.Contains(m.CourtNumber))
                .GroupBy(m => m.CourtNumber!)
                .ToDictionary(g => g.Key, g => g.First());

            // กลุ่ม B: ทีมสำรอง (CourtNumber เป็น Null หรือ "ไม่มีชื่อในสนามจริง" เช่น "-1", "-2")
            var generalStagedMatches = stagedMatches
                .Where(m => string.IsNullOrEmpty(m.CourtNumber) || !validCourtSet.Contains(m.CourtNumber))
                .ToList();

            // รวมแมตช์ที่จัดรอในสนาม (Staged - Matches on Courts) เข้าไปแสดงผลทับสนาม
            foreach (var stagedMatch in stagedMatchesForCourts.Values)
            {
                if (stagedMatch.CourtNumber != null)
                {
                    activeMatchesMap[stagedMatch.CourtNumber] = new CurrentlyPlayingMatchDto
                    {
                        MatchId = stagedMatch.MatchId,
                        CourtNumber = stagedMatch.CourtNumber,
                        StartTime = null, // ยังไม่เริ่ม
                        TeamA = stagedMatch.MatchPlayers.Where(p => p.Team == "A").Select(MapToPlayerInMatchDto).ToList(),
                        TeamB = stagedMatch.MatchPlayers.Where(p => p.Team == "B").Select(MapToPlayerInMatchDto).ToList()
                    };
                }
            }

            var courtStatuses = courtIdentifiers.Select(identifier => new CourtStatusDto
            {
                CourtIdentifier = identifier,
                CurrentMatch = activeMatchesMap.TryGetValue(identifier, out var match) ? match : null
            }).ToList();

            // --- (ส่วน Waiting Pool / ID Filtering) ---
            var playersInMatchIds = activeMatches
                .SelectMany(m => m.MatchPlayers).Where(mp => mp.UserId.HasValue).Select(mp => mp.UserId).ToHashSet();
            var playersInStagedMatchIds = stagedMatches
                .SelectMany(m => m.MatchPlayers).Where(mp => mp.UserId.HasValue).Select(mp => mp.UserId).ToHashSet();
            var walkinsInMatchIds = activeMatches
                .SelectMany(m => m.MatchPlayers).Where(mp => mp.WalkinId.HasValue).Select(mp => mp.WalkinId).ToHashSet();
            var walkinsInStagedMatchIds = stagedMatches
                .SelectMany(m => m.MatchPlayers).Where(mp => mp.WalkinId.HasValue).Select(mp => mp.WalkinId).ToHashSet();

            // --- NEW: นับจำนวนเกมที่เล่นจบแล้ว (Status = 2) ของแต่ละคน ---
            var finishedMatchPlayers = await _context.MatchPlayers
                .Where(mp => mp.Match.SessionId == sessionId && mp.Match.Status == 2)
                .Select(mp => new { mp.UserId, mp.WalkinId, EndTime = mp.Match.EndTime }) // เพิ่ม EndTime
                .ToListAsync();

            var memberGameCounts = finishedMatchPlayers
                .Where(mp => mp.UserId.HasValue)
                .GroupBy(mp => mp.UserId)
                .ToDictionary(g => g.Key!.Value, g => new { Count = g.Count(), LastPlayed = g.Max(x => x.EndTime) }); // เก็บเวลาเล่นล่าสุด

            var guestGameCounts = finishedMatchPlayers
                .Where(mp => mp.WalkinId.HasValue)
                .GroupBy(mp => mp.WalkinId)
                .ToDictionary(g => g.Key!.Value, g => new { Count = g.Count(), LastPlayed = g.Max(x => x.EndTime) }); // เก็บเวลาเล่นล่าสุด

            var waitingMembers = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId && p.CheckinTime != null && p.CheckoutTime == null && !playersInMatchIds.Contains(p.UserId) && !playersInStagedMatchIds.Contains(p.UserId))
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .Select(p => new WaitingPlayerDto
                {
                    ParticipantId = p.ParticipantId,
                    ParticipantType = ParticipantTypes.Member,
                    Nickname = p.User.UserProfile != null ? p.User.UserProfile.Nickname ?? "" : "",
                    ProfilePhotoUrl = p.User.UserProfile != null ? p.User.UserProfile.ProfilePhotoUrl : null,
                    GenderName = p.User.UserProfile == null ? "อื่นๆ" : (p.User.UserProfile.Gender == 1 ? "ชาย" : p.User.UserProfile.Gender == 2 ? "หญิง" : "อื่นๆ"),
                    SkillLevelId = p.SkillLevel != null ? p.SkillLevel.SkillLevelId : null,
                    SkillLevelName = p.SkillLevel != null ? p.SkillLevel.LevelName : null,
                    SkillLevelColor = p.SkillLevel != null ? p.SkillLevel.ColorHexCode : null,
                    // ถ้าเคยเล่นแล้ว ให้ใช้เวลาจบเกมล่าสุดเป็นเวลาเริ่มรอ ถ้ายังไม่เคยให้ใช้เวลา Checkin
                    CheckedInTime = (memberGameCounts.ContainsKey(p.UserId) && memberGameCounts[p.UserId].LastPlayed.HasValue && memberGameCounts[p.UserId].LastPlayed!.Value > p.CheckinTime!.Value)
                                    ? memberGameCounts[p.UserId].LastPlayed!.Value : p.CheckinTime!.Value,
                    TotalGamesPlayed = memberGameCounts.ContainsKey(p.UserId) ? memberGameCounts[p.UserId].Count : 0
                })
                .ToListAsync();

            var waitingGuests = await _context.SessionWalkinGuests
                    .Where(g => g.SessionId == sessionId && g.CheckinTime != null && g.CheckoutTime == null && !walkinsInMatchIds.Contains(g.WalkinId) && !walkinsInStagedMatchIds.Contains(g.WalkinId))
                    .Include(g => g.SkillLevel)
                    .Select(g => new WaitingPlayerDto
                    {
                        ParticipantId = g.WalkinId,
                        ParticipantType = ParticipantTypes.Guest,
                        Nickname = g.GuestName,
                        ProfilePhotoUrl = null,
                        GenderName = g.Gender == 1 ? "ชาย" : g.Gender == 2 ? "หญิง" : "อื่นๆ",
                        SkillLevelId = g.SkillLevel != null ? g.SkillLevel.SkillLevelId : null,
                        SkillLevelName = g.SkillLevel != null ? g.SkillLevel.LevelName : null,
                        SkillLevelColor = g.SkillLevel != null ? g.SkillLevel.ColorHexCode : null,
                        // ถ้าเคยเล่นแล้ว ให้ใช้เวลาจบเกมล่าสุดเป็นเวลาเริ่มรอ
                        CheckedInTime = (guestGameCounts.ContainsKey(g.WalkinId) && guestGameCounts[g.WalkinId].LastPlayed.HasValue && guestGameCounts[g.WalkinId].LastPlayed!.Value > g.CheckinTime!.Value)
                                        ? guestGameCounts[g.WalkinId].LastPlayed!.Value : g.CheckinTime!.Value,
                        TotalGamesPlayed = guestGameCounts.ContainsKey(g.WalkinId) ? guestGameCounts[g.WalkinId].Count : 0
                    })
                    .ToListAsync();

            var allWaitingPlayers = waitingMembers.Concat(waitingGuests).ToList();

            // 6. แปลง Staged Matches (ทีมสำรอง/General) เป็น DTO
            var stagedMatchesDto = generalStagedMatches.Select(match => new StagedMatchDto
            {
                MatchId = match.MatchId,
                CourtNumber = match.CourtNumber, // ส่งค่าเดิมกลับไป (เช่น "-1", "-2")
                TeamA = match.MatchPlayers.Where(p => p.Team == "A").Select(MapToPlayerInMatchDto).ToList(),
                TeamB = match.MatchPlayers.Where(p => p.Team == "B").Select(MapToPlayerInMatchDto).ToList()
            }).ToList();

            // --- จัดการชื่อซ้ำทั้งหมดใน Session ---
            // 1. รวบรวมผู้เล่นทั้งหมดจากทุกที่ (Courts, Staged, Waiting)
            var allPlayersInSession = new List<object>();
            allPlayersInSession.AddRange(courtStatuses.SelectMany(cs => cs.CurrentMatch?.TeamA ?? new List<PlayerInMatchDto>()));
            allPlayersInSession.AddRange(courtStatuses.SelectMany(cs => cs.CurrentMatch?.TeamB ?? new List<PlayerInMatchDto>()));
            allPlayersInSession.AddRange(stagedMatchesDto.SelectMany(sm => sm.TeamA));
            allPlayersInSession.AddRange(stagedMatchesDto.SelectMany(sm => sm.TeamB));
            allPlayersInSession.AddRange(allWaitingPlayers);

            // 3. เรียกใช้ฟังก์ชันกับผู้เล่นทั้งหมด
            ProcessDuplicateNames(allPlayersInSession.Cast<dynamic>());

            var result = new LiveSessionStateDto
            {
                groupName = session.GroupName,
                Courts = courtStatuses,
                WaitingPool = allWaitingPlayers.OrderBy(p => p.CheckedInTime).ToList(),
                StagedMatches = stagedMatchesDto,
                CompetitionStartTime = session.CompetitionStartTime
            };

            return result;
        }
    }
}
