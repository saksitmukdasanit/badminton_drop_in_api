using Microsoft.Extensions.Configuration;
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
        public async Task<CurrentlyPlayingMatchDto> StartMatchAsync(int sessionId, int organizerUserId, CreateMatchDto dto)
        {
            // 1. ตรวจสอบสิทธิ์
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId) throw new Exception("Unauthorized");

            // 2. สร้างและบันทึก Match หลัก
            var match = new Match
            {
                SessionId = sessionId,
                CourtNumber = dto.CourtNumber,
                StartTime = DateTime.UtcNow,
                Status = 1, // 1=กำลังเล่น
                CreatedBy = organizerUserId,
            };
            await _context.Matches.AddAsync(match);
            await _context.SaveChangesAsync();

            // 3. ดึง UserId จาก ParticipantId สำหรับผู้เล่นที่เป็น Member
            var memberParticipantIds = dto.TeamA.Concat(dto.TeamB)
                .Where(p => string.Equals(p.Type, "Member", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Id)
                .ToList();

            var memberUserIdMap = await _context.SessionParticipants
                .Where(sp => memberParticipantIds.Contains(sp.ParticipantId))
                .ToDictionaryAsync(sp => sp.ParticipantId, sp => sp.UserId);

            // 4. สร้างและบันทึกผู้เล่นใน Match (MatchPlayer)
            var matchPlayers = new List<MatchPlayer>();
            foreach (var p in dto.TeamA)
            {
                int? userId = string.Equals(p.Type, "Member", StringComparison.OrdinalIgnoreCase) && memberUserIdMap.ContainsKey(p.Id) ? memberUserIdMap[p.Id] : null;
                int? walkinId = string.Equals(p.Type, "Guest", StringComparison.OrdinalIgnoreCase) ? p.Id : null;
                matchPlayers.Add(new MatchPlayer { MatchId = match.MatchId, Team = "A", UserId = userId, WalkinId = walkinId });
            }
            foreach (var p in dto.TeamB)
            {
                int? userId = string.Equals(p.Type, "Member", StringComparison.OrdinalIgnoreCase) && memberUserIdMap.ContainsKey(p.Id) ? memberUserIdMap[p.Id] : null;
                int? walkinId = string.Equals(p.Type, "Guest", StringComparison.OrdinalIgnoreCase) ? p.Id : null;
                matchPlayers.Add(new MatchPlayer { MatchId = match.MatchId, Team = "B", UserId = userId, WalkinId = walkinId });
            }

            await _context.MatchPlayers.AddRangeAsync(matchPlayers);
            await _context.SaveChangesAsync();

            // --- แจ้งเตือนผู้เล่นว่ากำลังจะได้ลงสนาม ---
            foreach (var player in matchPlayers)
            {
                if (player.UserId.HasValue)
                {
                    await _notificationService.SendNotificationAsync(
                        player.UserId.Value,
                        "ถึงเวลาลงสนาม!",
                        $"คุณกำลังจะเริ่มแข่งในสนาม {match.CourtNumber} ของก๊วน '{session.GroupName}'",
                        "MATCH_STARTING",
                        sessionId
                    );
                }
            }

            // 5. เตรียมข้อมูลเพื่อส่งกลับ (CurrentlyPlayingMatchDto)
            var allPlayersInMatch = await _context.MatchPlayers
                .Where(mp => mp.MatchId == match.MatchId)
                .Include(mp => mp.User!).ThenInclude(u => u.UserProfile)
                .Include(mp => mp.User!).ThenInclude(u => u.SessionParticipants.Where(sp => sp.SessionId == sessionId)).ThenInclude(sp => sp.SkillLevel)
                .Include(mp => mp.Walkin).ThenInclude(w => w!.SkillLevel)
                .ToListAsync();

            Func<MatchPlayer, PlayerInMatchDto> createPlayerDto = p =>
            {
                if (p.UserId.HasValue)
                {
                    var sessionParticipant = p.User?.SessionParticipants.FirstOrDefault();
                    return new PlayerInMatchDto
                    {
                        UserId = sessionParticipant?.ParticipantId ?? p.UserId, // FIX: ส่ง ParticipantId กลับไป
                        Nickname = p.User?.UserProfile?.Nickname ?? "N/A",
                        ProfilePhotoUrl = p.User?.UserProfile?.ProfilePhotoUrl,
                        GenderName = p.User?.UserProfile?.Gender == 1 ? "ชาย" : p.User?.UserProfile?.Gender == 2 ? "หญิง" : "อื่นๆ",
                        SkillLevelId = sessionParticipant?.SkillLevelId,
                        SkillLevelName = sessionParticipant?.SkillLevel?.LevelName,
                        SkillLevelColor = sessionParticipant?.SkillLevel?.ColorHexCode,
                        EmergencyContactName = p.User?.UserProfile?.EmergencyContactName,
                        EmergencyContactPhone = p.User?.UserProfile?.EmergencyContactPhone
                    };
                }
                else // WalkinId.HasValue
                {
                    return new PlayerInMatchDto
                    {
                        WalkinId = p.WalkinId,
                        Nickname = p.Walkin?.GuestName ?? "N/A",
                        GenderName = p.Walkin?.Gender == 1 ? "ชาย" : p.Walkin?.Gender == 2 ? "หญิง" : "อื่นๆ",
                        SkillLevelId = p.Walkin?.SkillLevelId,
                        SkillLevelName = p.Walkin?.SkillLevel?.LevelName,
                        SkillLevelColor = p.Walkin?.SkillLevel?.ColorHexCode
                    };
                }
            };

            // 6. Map ข้อมูลไปยัง DTO ที่จะส่งกลับ
            var matchDto = new CurrentlyPlayingMatchDto
            {
                MatchId = match.MatchId,
                CourtNumber = match.CourtNumber,
                StartTime = match.StartTime.Value,
                TeamA = allPlayersInMatch.Where(p => p.Team == "A").Select(createPlayerDto).ToList(),
                TeamB = allPlayersInMatch.Where(p => p.Team == "B").Select(createPlayerDto).ToList()
            };

            // Broadcast state change
            await BroadcastLiveStateChange(sessionId, organizerUserId);

            return matchDto;
        }

        public async Task<bool> EndMatchAsync(int matchId, int organizerUserId)
        {
            var match = await _context.Matches
                .Include(m => m.Session)
                .FirstOrDefaultAsync(m => m.MatchId == matchId);

            if (match == null) return false;

            // ตรวจสอบสิทธิ์ว่าเป็นผู้จัดของก๊วนนี้
            if (match.Session.CreatedByUserId != organizerUserId)
            {
                return false; // ไม่มีสิทธิ์
            }

            match.Status = 2; // 2=จบแล้ว
            match.EndTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Broadcast state change
            await BroadcastLiveStateChange(match.SessionId, organizerUserId);
            return true;
        }
        public async Task<StagedMatchDto?> CreateStagedMatchAsync(int sessionId, int organizerUserId, CreateStagedMatchDto dto)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId) return null;

            // กำหนด Target Court ID: ถ้าส่งมาเป็น Null ให้ถือว่าเป็น -1 (String)
            string targetCourtId = dto.courtIdentifier ?? "-1";

            // 1. ค้นหา Match ตาม courtIdentifier ที่ระบุ
            var match = await _context.Matches
                .Include(m => m.MatchPlayers)
                .FirstOrDefaultAsync(m => m.SessionId == sessionId &&
                                            m.Status == 4 && // Staged
                                            m.CourtNumber == targetCourtId);

            // 2. ถ้ายังไม่มี Match ให้สร้างใหม่
            if (match == null)
            {
                match = new Match
                {
                    SessionId = sessionId,
                    CourtNumber = targetCourtId,
                    Status = 4, // Staged
                    CreatedBy = organizerUserId,
                    CreatedDate = DateTime.UtcNow
                };
                await _context.Matches.AddAsync(match);
                await _context.SaveChangesAsync();
            }
            else
            {
                // *** Full Sync Logic: ล้างผู้เล่นเดิมออก เพื่อบันทึกชุดใหม่ที่ส่งมา ***
                if (match.MatchPlayers != null && match.MatchPlayers.Any())
                {
                    _context.MatchPlayers.RemoveRange(match.MatchPlayers);
                    await _context.SaveChangesAsync(); // บันทึกการล้างผู้เล่นเก่าทันที
                }
            }

            // 3. เตรียมข้อมูลผู้เล่นใหม่ (New Players)
            var validTeamA = dto.TeamA?.Where(p => p != null).ToList() ?? new List<PlayerSelectionDto>();
            var validTeamB = dto.TeamB?.Where(p => p != null).ToList() ?? new List<PlayerSelectionDto>();

            // ถ้าไม่มีผู้เล่นส่งมาเลย และมี match อยู่ ให้ลบ match นั้นทิ้ง
            if (!validTeamA.Any() && !validTeamB.Any())
            {
                if (match != null)
                {
                    if (match.MatchPlayers != null && match.MatchPlayers.Any())
                    {
                        _context.MatchPlayers.RemoveRange(match.MatchPlayers);
                    }
                    _context.Matches.Remove(match);
                    await _context.SaveChangesAsync();
                }

                // --- FIX: เพิ่มการ Broadcast เพื่อให้แอปฝั่งผู้เล่นรับรู้ว่าทีมถูกลบ/คนถูกย้ายออกหมดแล้ว ---
                await BroadcastLiveStateChange(sessionId, organizerUserId);

                return null; // ไม่มีผู้เล่น ไม่ต้องสร้าง DTO
            }

            // // ตรวจสอบว่าผู้เล่นที่ส่งมาไม่ได้อยู่ใน Staged Match อื่น (ทั้งในสนามจริงและทีมสำรอง)
            // var allPlayersInDto = validTeamA.Concat(validTeamB).ToList();
            // var memberParticipantIds = allPlayersInDto.Where(p => p.Type == "Member").Select(p => p.Id).ToList();
            // var guestWalkinIds = allPlayersInDto.Where(p => p.Type == "Guest").Select(p => p.Id).ToList();
            //
            // var memberUserIdsInDto = await _context.SessionParticipants
            //     .Where(sp => memberParticipantIds.Contains(sp.ParticipantId))
            //     .Select(sp => sp.UserId)
            //     .ToListAsync();
            //
            // // ดึงผู้เล่นทั้งหมดที่อยู่ใน Staged Match อื่นๆ (ที่ไม่ใช่ targetCourtId)
            // var playersInOtherStagedMatches = await _context.MatchPlayers
            //     .Where(mp => mp.Match.SessionId == sessionId &&
            //                  mp.Match.Status == 4 && // Staged
            //                  mp.Match.CourtNumber != targetCourtId) // Match อื่นๆ
            //     .Select(mp => new { mp.UserId, mp.WalkinId })
            //     .ToListAsync();
            //
            // var otherStagedUserIds = playersInOtherStagedMatches.Where(p => p.UserId.HasValue).Select(p => p.UserId.Value).ToHashSet();
            // var otherStagedWalkinIds = playersInOtherStagedMatches.Where(p => p.WalkinId.HasValue).Select(p => p.WalkinId.Value).ToHashSet();
            //
            // var isPlayerInAnotherStagedMatch = memberUserIdsInDto.Any(id => otherStagedUserIds.Contains(id)) ||
            //                                    guestWalkinIds.Any(id => otherStagedWalkinIds.Contains(id));
            //
            // if (isPlayerInAnotherStagedMatch) return null; // ผู้เล่นอยู่ในสนามอื่นแล้ว

            var allMemberIds = validTeamA.Concat(validTeamB)
                .Where(p => string.Equals(p.Type, "Member", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Id)
                .Distinct()
                .ToList();

            var memberUserIdMap = await _context.SessionParticipants
                .Where(sp => allMemberIds.Contains(sp.ParticipantId))
                .ToDictionaryAsync(sp => sp.ParticipantId, sp => sp.UserId);

            var newMatchPlayers = new List<MatchPlayer>();

            void PreparePlayers(List<PlayerSelectionDto> players, string team)
            {
                foreach (var p in players)
                {
                    if (p == null) continue;

                    int? userId = null;
                    int? walkinId = null;

                    if (string.Equals(p.Type, "Member", StringComparison.OrdinalIgnoreCase) && memberUserIdMap.ContainsKey(p.Id))
                    {
                        userId = memberUserIdMap[p.Id];
                    }
                    else if (string.Equals(p.Type, "Guest", StringComparison.OrdinalIgnoreCase))
                    {
                        walkinId = p.Id;
                    }

                    if (userId.HasValue || walkinId.HasValue)
                    {
                        newMatchPlayers.Add(new MatchPlayer
                        {
                            MatchId = match.MatchId,
                            Team = team,
                            UserId = userId,
                            WalkinId = walkinId
                        });
                    }
                }
            }

            PreparePlayers(validTeamA, "A");
            PreparePlayers(validTeamB, "B");

            if (newMatchPlayers.Any())
            {
                await _context.MatchPlayers.AddRangeAsync(newMatchPlayers);
            }

            await _context.SaveChangesAsync();

            // 5. Return DTO
            var createdMatchPlayers = await _context.MatchPlayers
                .Where(mp => mp.MatchId == match.MatchId)
                .Include(mp => mp.User!).ThenInclude(u => u.UserProfile)
                .Include(mp => mp.User!).ThenInclude(u => u.SessionParticipants.Where(sp => sp.SessionId == sessionId)).ThenInclude(sp => sp.SkillLevel)
                .Include(mp => mp.Walkin).ThenInclude(w => w!.SkillLevel)
                .ToListAsync();

            var stagedMatchDto = new StagedMatchDto
            {
                MatchId = match.MatchId,
                CourtNumber = match.CourtNumber,
                TeamA = createdMatchPlayers.Where(p => p.Team == "A").Select(p => new PlayerInMatchDto
                {
                    UserId = p.User?.SessionParticipants.FirstOrDefault()?.ParticipantId ?? p.UserId, // FIX: ส่ง ParticipantId
                    WalkinId = p.WalkinId,
                    Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                    ProfilePhotoUrl = p.UserId.HasValue ? p.User?.UserProfile?.ProfilePhotoUrl : null,
                    GenderName = p.UserId.HasValue ? (p.User?.UserProfile?.Gender == 1 ? "ชาย" : p.User?.UserProfile?.Gender == 2 ? "หญิง" : "อื่นๆ") : (p.Walkin?.Gender == 1 ? "ชาย" : p.Walkin?.Gender == 2 ? "หญิง" : "อื่นๆ"),
                    SkillLevelId = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault()?.SkillLevelId : p.Walkin?.SkillLevelId,
                    SkillLevelName = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault()?.SkillLevel?.LevelName : p.Walkin?.SkillLevel?.LevelName,
                    SkillLevelColor = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault()?.SkillLevel?.ColorHexCode : p.Walkin?.SkillLevel?.ColorHexCode,
                    EmergencyContactName = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactName : null,
                    EmergencyContactPhone = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactPhone : null
                }).ToList(),
                TeamB = createdMatchPlayers.Where(p => p.Team == "B").Select(p => new PlayerInMatchDto
                {
                    UserId = p.User?.SessionParticipants.FirstOrDefault()?.ParticipantId ?? p.UserId, // FIX: ส่ง ParticipantId
                    WalkinId = p.WalkinId,
                    Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                    ProfilePhotoUrl = p.UserId.HasValue ? p.User?.UserProfile?.ProfilePhotoUrl : null,
                    GenderName = p.UserId.HasValue ? (p.User?.UserProfile?.Gender == 1 ? "ชาย" : p.User?.UserProfile?.Gender == 2 ? "หญิง" : "อื่นๆ") : (p.Walkin?.Gender == 1 ? "ชาย" : p.Walkin?.Gender == 2 ? "หญิง" : "อื่นๆ"),
                    SkillLevelId = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault()?.SkillLevelId : p.Walkin?.SkillLevelId,
                    SkillLevelName = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault()?.SkillLevel?.LevelName : p.Walkin?.SkillLevel?.LevelName,
                    SkillLevelColor = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault()?.SkillLevel?.ColorHexCode : p.Walkin?.SkillLevel?.ColorHexCode,
                    EmergencyContactName = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactName : null,
                    EmergencyContactPhone = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactPhone : null
                }).ToList()
            };

            // Broadcast state change
            await BroadcastLiveStateChange(sessionId, organizerUserId);

            return stagedMatchDto;
        }

        public async Task<CurrentlyPlayingMatchDto?> StartStagedMatchAsync(int matchId, int organizerUserId, StartStagedMatchDto dto)
        {
            var match = await _context.Matches
                .Include(m => m.Session)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User!).ThenInclude(u => u.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User!).ThenInclude(u => u.SessionParticipants).ThenInclude(sp => sp.SkillLevel)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin).ThenInclude(w => w!.SkillLevel)
                .FirstOrDefaultAsync(m => m.MatchId == matchId);

            if (match == null || match.Session.CreatedByUserId != organizerUserId || match.Status != 4)
            {
                return null;
            }

            // ใช้ CourtNumber ที่อยู่ใน Match (ถ้ามี) หรือจาก DTO
            var courtNumberToAssign = match.CourtNumber ?? dto.CourtNumber;

            // ตรวจสอบว่าสนามที่จะเริ่มแข่งว่างอยู่หรือไม่ (เช็คแบบ String)
            if (!string.IsNullOrEmpty(courtNumberToAssign))
            {
                var isCourtOccupied = await _context.Matches.AnyAsync(m => m.SessionId == match.SessionId && m.Status == 1 && m.CourtNumber == courtNumberToAssign);
                if (isCourtOccupied)
                {
                    return null; // สนามไม่ว่าง
                }
            }
            else
            {
                // ถ้าไม่มีการระบุ CourtNumber มาเลย ก็ไม่สามารถเริ่มได้
                return null;
            }

            match.Status = 1; // 1=Playing
            match.CourtNumber = courtNumberToAssign;
            match.StartTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // --- แจ้งเตือนผู้เล่นว่ากำลังจะได้ลงสนาม ---
            foreach (var player in match.MatchPlayers)
            {
                if (player.UserId.HasValue)
                {
                    await _notificationService.SendNotificationAsync(
                        player.UserId.Value,
                        "ถึงเวลาลงสนาม!",
                        $"คุณกำลังจะเริ่มแข่งในสนาม {match.CourtNumber} ของก๊วน '{match.Session.GroupName}'",
                        "MATCH_STARTING",
                        match.SessionId
                    );
                }
            }

            var matchDto = new CurrentlyPlayingMatchDto
            {
                MatchId = match.MatchId,
                CourtNumber = match.CourtNumber,
                StartTime = match.StartTime.Value,
                TeamA = match.MatchPlayers.Where(p => p.Team == "A").Select(p => new PlayerInMatchDto
                {
                    UserId = p.User?.SessionParticipants.FirstOrDefault(sp => sp.SessionId == match.SessionId)?.ParticipantId ?? p.UserId, // FIX: ส่ง ParticipantId
                    WalkinId = p.WalkinId,
                    Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                    ProfilePhotoUrl = p.UserId.HasValue ? p.User?.UserProfile?.ProfilePhotoUrl : null,
                    GenderName = p.UserId.HasValue ? (p.User?.UserProfile?.Gender == 1 ? "ชาย" : p.User?.UserProfile?.Gender == 2 ? "หญิง" : "อื่นๆ") : (p.Walkin?.Gender == 1 ? "ชาย" : p.Walkin?.Gender == 2 ? "หญิง" : "อื่นๆ"),
                    SkillLevelId = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault(sp => sp.SessionId == match.SessionId)?.SkillLevelId : p.Walkin?.SkillLevelId,
                    SkillLevelName = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault(sp => sp.SessionId == match.SessionId)?.SkillLevel?.LevelName : p.Walkin?.SkillLevel?.LevelName,
                    SkillLevelColor = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault(sp => sp.SessionId == match.SessionId)?.SkillLevel?.ColorHexCode : p.Walkin?.SkillLevel?.ColorHexCode,
                    EmergencyContactName = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactName : null,
                    EmergencyContactPhone = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactPhone : null
                }).ToList(),
                TeamB = match.MatchPlayers.Where(p => p.Team == "B").Select(p => new PlayerInMatchDto
                {
                    UserId = p.User?.SessionParticipants.FirstOrDefault(sp => sp.SessionId == match.SessionId)?.ParticipantId ?? p.UserId, // FIX: ส่ง ParticipantId
                    WalkinId = p.WalkinId,
                    Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                    ProfilePhotoUrl = p.UserId.HasValue ? p.User?.UserProfile?.ProfilePhotoUrl : null,
                    GenderName = p.UserId.HasValue ? (p.User?.UserProfile?.Gender == 1 ? "ชาย" : p.User?.UserProfile?.Gender == 2 ? "หญิง" : "อื่นๆ") : (p.Walkin?.Gender == 1 ? "ชาย" : p.Walkin?.Gender == 2 ? "หญิง" : "อื่นๆ"),
                    SkillLevelId = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault(sp => sp.SessionId == match.SessionId)?.SkillLevelId : p.Walkin?.SkillLevelId,
                    SkillLevelName = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault(sp => sp.SessionId == match.SessionId)?.SkillLevel?.LevelName : p.Walkin?.SkillLevel?.LevelName,
                    SkillLevelColor = p.UserId.HasValue ? p.User?.SessionParticipants.FirstOrDefault(sp => sp.SessionId == match.SessionId)?.SkillLevel?.ColorHexCode : p.Walkin?.SkillLevel?.ColorHexCode,
                    EmergencyContactName = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactName : null,
                    EmergencyContactPhone = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactPhone : null
                }).ToList()
            };

            // Broadcast state change
            await BroadcastLiveStateChange(match.SessionId, organizerUserId);

            return matchDto;
        }
    }
}
