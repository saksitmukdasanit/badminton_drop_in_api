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
        public async Task<(bool Success, string Message)> CheckinParticipantAsync(int sessionId, int organizerUserId, CheckinDto dto)
        {
            var session = await _context.GameSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);
            if (session == null) return (false, "Session not found or you do not have permission.");

            if (dto.ParticipantId.HasValue && !string.IsNullOrEmpty(dto.ParticipantType))
            {
                if (dto.ParticipantType.Equals(ParticipantTypes.Member, StringComparison.OrdinalIgnoreCase))
                {
                    var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.ParticipantId == dto.ParticipantId.Value);
                    if (participant == null) return (false, "Member not found in this session.");
                    // if (participant.CheckinTime != null) return (false, "Member already checked in."); // อนุญาตให้ Check-in ซ้ำได้

                    participant.CheckinTime = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // 1. อัปเดตกระดาน (Live State) ให้คนอื่นๆ ในก๊วนเห็น
                    await BroadcastLiveStateChange(sessionId, organizerUserId);
                    // 2. ส่ง Event เฉพาะกิจบอกแอปฝั่งผู้เล่นให้รู้ตัวว่าเช็คอินแล้ว
                    await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("PlayerCheckedIn", participant.UserId);

                    return (true, "Member checked in successfully.");
                }
                else if (dto.ParticipantType.Equals(ParticipantTypes.Guest, StringComparison.OrdinalIgnoreCase))
                {
                    var guest = await _context.SessionWalkinGuests.FirstOrDefaultAsync(g => g.SessionId == sessionId && g.WalkinId == dto.ParticipantId.Value);
                    if (guest == null) return (false, "Guest not found in this session.");
                    // if (guest.CheckinTime != null) return (false, "Guest already checked in."); // อนุญาตให้ Check-in ซ้ำได้

                    guest.CheckinTime = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // แขก Walk-in ไม่มีแอปของตัวเอง อัปเดตแค่กระดานพอ
                    await BroadcastLiveStateChange(sessionId, organizerUserId);
                    return (true, "Guest checked in successfully.");
                }
                else
                {
                    return (false, "Invalid participant type.");
                }
            }
            else if (!string.IsNullOrEmpty(dto.ScannedData))
            {
                // ตรวจสอบว่าสิ่งที่สแกนมาเป็นตัวเลข (UserId) หรือไม่
                bool isNumeric = int.TryParse(dto.ScannedData, out int scannedUserId);

                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.UserPublicId.ToString() == dto.ScannedData ||
                    (isNumeric && u.UserId == scannedUserId));

                if (user == null) return (false, "User not found from QR code.");

                var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == user.UserId);
                if (participant == null) return (false, "This user is not registered for this session.");
                // if (participant.CheckinTime != null) return (false, "User already checked in."); // อนุญาตให้ Check-in ซ้ำได้

                participant.CheckinTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // 1. อัปเดตกระดาน (Live State) 
                await BroadcastLiveStateChange(sessionId, organizerUserId);
                // 2. ส่ง Event บังคับเด้งหน้าจอให้แอปผู้เล่น
                await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("PlayerCheckedIn", user.UserId);

                return (true, "Check-in successful.");
            }

            return (false, "Invalid check-in data provided.");
        }

        // ฟังก์ชันค้นหาประวัติแขกเดิม (Autocomplete)
        public async Task<List<GuestSuggestionDto>> SearchPreviousGuestsAsync(int organizerUserId, string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<GuestSuggestionDto>();

            var term = query.Trim().ToLower();

            // ค้นหาจากประวัติ Walk-in ของ Organizer คนนี้ โดยดูจาก Session ที่เขาเป็นคนสร้าง
            var guests = await _context.SessionWalkinGuests
                .AsNoTracking() // เพิ่ม AsNoTracking เพื่อประสิทธิภาพ
                .Include(g => g.Session)
                // FIX: เปลี่ยนกลับมาใช้ Contains + ToLower ซึ่งเสถียรกว่า Like ในหลาย Database
                .Where(g => (g.Session.CreatedByUserId == organizerUserId || g.CreatedBy == organizerUserId) &&
                            (g.GuestName.Contains(term) ||
                             (g.PhoneNumber != null && g.PhoneNumber.Contains(term))))
                .OrderByDescending(g => g.CreatedDate) // เอาล่าสุดขึ้นก่อน
                .Select(g => new GuestSuggestionDto
                {
                    GuestName = g.GuestName,
                    PhoneNumber = g.PhoneNumber,
                    Gender = g.Gender != null ? (int)g.Gender : 1,
                    SkillLevelId = g.SkillLevelId
                })
                .ToListAsync();

            // Group by ชื่อ เพื่อไม่ให้ซ้ำ และเอาข้อมูลล่าสุด
            return guests.GroupBy(g => g.GuestName).Select(g => g.First()).Take(10).ToList();
        }

        public async Task<WaitingPlayerDto> AddWalkinGuestAsync(int sessionId, int organizerUserId, AddWalkinDto dto)
        {
            var newGuest = new SessionWalkinGuest
            {
                SessionId = sessionId,
                GuestName = dto.GuestName,
                PhoneNumber = dto.PhoneNumber,
                Gender = (short?)dto.Gender,
                SkillLevelId = dto.SkillLevelId,
                Status = 1,
                CreatedBy = organizerUserId, // FIX: บันทึกคนสร้าง เพื่อให้ค้นหาเจอในอนาคต
                CheckinTime = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow
            };
            await _context.SessionWalkinGuests.AddAsync(newGuest);
            await _context.SaveChangesAsync();

            var skillLevel = newGuest.SkillLevelId.HasValue
                ? await _context.OrganizerSkillLevels.FindAsync(newGuest.SkillLevelId.Value)
                : null;

            return new WaitingPlayerDto
            {
                ParticipantId = newGuest.WalkinId,
                ParticipantType = ParticipantTypes.Guest,
                Nickname = newGuest.GuestName,
                ProfilePhotoUrl = null,
                SkillLevelName = skillLevel?.LevelName,
                SkillLevelColor = skillLevel?.ColorHexCode,
                CheckedInTime = newGuest.CheckinTime.Value
            };
        }

        public async Task<bool> UpdateParticipantSkillAsync(string participantType, int participantId, UpdateParticipantSkillDto dto)
        {
            int sessionId = 0;
            int organizerUserId = 0;

            if (ParticipantTypes.IsMember(participantType))
            {
                // 1. Find the session participant to get session and user info
                var participant = await _context.SessionParticipants
                    .Include(p => p.Session) // Need session to get OrganizerId
                    .FirstOrDefaultAsync(p => p.ParticipantId == participantId);

                if (participant == null) return false;

                sessionId = participant.SessionId;
                organizerUserId = participant.Session.CreatedByUserId;

                // 2. Update the skill for the current session (as before)
                participant.SkillLevelId = dto.SkillLevelId;

                // --- NEW LOGIC: Save the skill level for the member globally for this organizer ---
                int memberUserId = participant.UserId;

                // Find if a skill record already exists for this member with this organizer
                var memberSkill = await _context.UserOrganizerSkills
                    .FirstOrDefaultAsync(uos => uos.OrganizerUserId == organizerUserId && uos.UserId == memberUserId);

                if (memberSkill != null)
                {
                    // Update existing record
                    if (dto.SkillLevelId > 0)
                    {
                        memberSkill.SkillLevelId = dto.SkillLevelId;
                        memberSkill.UpdatedDate = DateTime.UtcNow;
                        memberSkill.UpdatedBy = organizerUserId;
                    }
                }
                else if (dto.SkillLevelId > 0) // Only create if a skill is actually set
                {
                    // Create new record
                    var newMemberSkill = new UserOrganizerSkill
                    {
                        OrganizerUserId = organizerUserId,
                        UserId = memberUserId,
                        SkillLevelId = dto.SkillLevelId,
                        UpdatedDate = DateTime.UtcNow,
                        UpdatedBy = organizerUserId
                    };
                    await _context.UserOrganizerSkills.AddAsync(newMemberSkill);
                }
            }
            else if (ParticipantTypes.IsGuest(participantType))
            {
                var guest = await _context.SessionWalkinGuests
                    .Include(g => g.Session) // FIX: Include Session เพื่อเอา ID ไป Broadcast
                    .FirstOrDefaultAsync(g => g.WalkinId == participantId);

                if (guest == null) return false;

                sessionId = guest.SessionId;
                organizerUserId = guest.Session.CreatedByUserId;

                // ถ้าส่งมาเป็น 0 หรือน้อยกว่า ให้ถือว่าเป็น null (เคลียร์ค่า)
                guest.SkillLevelId = dto.SkillLevelId > 0 ? dto.SkillLevelId : null;
            }
            else
            {
                return false;
            }

            await _context.SaveChangesAsync();

            // --- NEW: Broadcast การเปลี่ยนแปลงเพื่อให้หน้าจออัปเดตทันที ---
            if (sessionId > 0)
            {
                await BroadcastLiveStateChange(sessionId, organizerUserId);
            }

            return true;
        }

        public async Task<PlayerSessionStatsDto?> GetPlayerSessionStatsAsync(int sessionId, string participantType, int participantId)
        {
            int? targetUserId = null;
            int? targetWalkinId = null;
            string nickname;

            if (ParticipantTypes.IsMember(participantType))
            {
                var participant = await _context.SessionParticipants
                    .Include(p => p.User.UserProfile)
                    .FirstOrDefaultAsync(p => p.ParticipantId == participantId && p.SessionId == sessionId);
                if (participant == null) return null;
                targetUserId = participant.UserId;
                nickname = participant.User.UserProfile?.Nickname ?? "";
            }
            else if (ParticipantTypes.IsGuest(participantType))
            {
                var guest = await _context.SessionWalkinGuests
                    .FirstOrDefaultAsync(g => g.WalkinId == participantId && g.SessionId == sessionId);
                if (guest == null) return null;
                targetWalkinId = guest.WalkinId;
                nickname = guest.GuestName;
            }
            else
            {
                return null;
            }

            var playedMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId &&
                            (m.Status == 1 || m.Status == 2) && // FIX: เอาเฉพาะเกมที่กำลังเล่นหรือจบแล้ว (ไม่เอา Cancelled/Staged)
                            m.MatchPlayers.Any(mp => (targetUserId.HasValue && mp.UserId == targetUserId) ||
                                                     (targetWalkinId.HasValue && mp.WalkinId == targetWalkinId)
                                               ))
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User!).ThenInclude(u => u.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin)
                .OrderByDescending(m => m.StartTime)
                .ToListAsync();

            var stats = new PlayerSessionStatsDto
            {
                ParticipantId = participantId,
                ParticipantType = participantType,
                Nickname = nickname,
                MatchHistory = new List<PlayerMatchHistoryDto>()
            };

            int totalMinutesPlayed = 0;
            int finishedGamesCount = 0; // NEW: ตัวนับเกมที่จบจริง

            foreach (var match in playedMatches)
            {
                var targetPlayerInMatch = match.MatchPlayers
                    .FirstOrDefault(mp => (targetUserId.HasValue && mp.UserId == targetUserId) ||
                                           (targetWalkinId.HasValue && mp.WalkinId == targetWalkinId));

                if (targetPlayerInMatch == null)
                {
                    continue;
                }

                var teammate = match.MatchPlayers
                    .FirstOrDefault(mp => mp.Team == targetPlayerInMatch.Team && mp.MatchPlayerId != targetPlayerInMatch.MatchPlayerId);

                var opponents = match.MatchPlayers
                    .Where(mp => mp.Team != targetPlayerInMatch.Team && mp.MatchPlayerId != targetPlayerInMatch.MatchPlayerId)
                    .ToList();

                var historyItem = new PlayerMatchHistoryDto
                {
                    MatchId = match.MatchId,
                    CourtNumber = match.CourtNumber,
                    StartTime = match.StartTime ?? DateTime.MinValue,
                    EndTime = match.EndTime ?? DateTime.UtcNow,
                    Teammate = teammate != null ? new PlayerInMatchDto
                    {
                        UserId = teammate.UserId,
                        WalkinId = teammate.WalkinId,
                        Nickname = teammate.UserId.HasValue ? teammate.User?.UserProfile?.Nickname ?? "N/A" : teammate.Walkin?.GuestName ?? "N/A",
                        ProfilePhotoUrl = teammate.UserId.HasValue ? teammate.User?.UserProfile?.ProfilePhotoUrl : null
                    } : new PlayerInMatchDto { Nickname = "N/A" },
                    Opponents = opponents.Select(o => new PlayerInMatchDto
                    {
                        UserId = o.UserId,
                        WalkinId = o.WalkinId,
                        Nickname = o.UserId.HasValue ? o.User?.UserProfile?.Nickname ?? "N/A" : o.Walkin?.GuestName ?? "N/A",
                        ProfilePhotoUrl = o.UserId.HasValue ? o.User?.UserProfile?.ProfilePhotoUrl : null
                    }).ToList()
                };

                if (match.Status == 2 && match.EndTime.HasValue && match.StartTime.HasValue)
                {
                    historyItem.DurationMinutes = (int)(match.EndTime.Value - match.StartTime.Value).TotalMinutes;
                    finishedGamesCount++; // FIX: นับเฉพาะเกมที่จบแล้ว
                    totalMinutesPlayed += historyItem.DurationMinutes;

                    historyItem.Result = targetPlayerInMatch.Result switch
                    {
                        1 => "Win",
                        2 => "Loss",
                        3 => "Draw",
                        _ => "N/A"
                    };

                    if (targetPlayerInMatch.Result == 1) stats.Wins++;
                    if (targetPlayerInMatch.Result == 2) stats.Losses++;
                }

                stats.MatchHistory.Add(historyItem);
            }

            stats.TotalGamesPlayed = finishedGamesCount; // FIX: ใช้ค่าที่นับจากเกมที่จบแล้วเท่านั้น
            stats.TotalMinutesPlayed = FormatTotalMinutes(totalMinutesPlayed);
            return stats;
        }

        private string FormatTotalMinutes(int totalMinutes)
        {
            if (totalMinutes <= 0)
            {
                return "0 นาที";
            }

            var timeSpan = TimeSpan.FromMinutes(totalMinutes);
            var hours = (int)timeSpan.TotalHours;
            var minutes = timeSpan.Minutes;

            if (hours > 0 && minutes > 0) return $"{hours} ชม. {minutes} นาที";
            if (hours > 0) return $"{hours} ชม.";

            return $"{minutes} นาที";
        }
    }
}
