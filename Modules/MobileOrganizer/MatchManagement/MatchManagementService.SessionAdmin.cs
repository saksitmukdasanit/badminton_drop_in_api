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
        public async Task<IEnumerable<SessionRosterPlayerDto>?> GetSessionRosterAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId)
            {
                return null;
            }

            var members = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId)
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .OrderBy(p => p.CheckinTime)
                .Select(p => new SessionRosterPlayerDto
                {
                    ParticipantId = p.ParticipantId,
                    ParticipantType = ParticipantTypes.Member,
                    Nickname = p.User.UserProfile != null ? (p.User.UserProfile.Nickname ?? "") : "",
                    FullName = p.User.UserProfile != null
                        ? ((p.User.UserProfile.FirstName ?? "") + " " + (p.User.UserProfile.LastName ?? "")).Trim()
                        : "",
                    Gender = p.User.UserProfile != null && p.User.UserProfile.Gender == 1 ? "ชาย" :
                             p.User.UserProfile != null && p.User.UserProfile.Gender == 2 ? "หญิง" :
                             p.User.UserProfile != null && p.User.UserProfile.Gender == 3 ? "อื่นๆ" : "ไม่ระบุ",
                    SkillLevelId = p.SkillLevelId,
                    SkillLevelName = p.SkillLevel != null ? p.SkillLevel.LevelName : null,
                    SkillLevelColor = p.SkillLevel != null ? p.SkillLevel.ColorHexCode : null,
                    IsCheckedIn = p.CheckinTime != null,
                    Status = (byte)(p.CheckoutTime != null ? (p.Status ?? 1) + 10 : (p.Status ?? 1)) // NEW: Map status & Checkout
                })
                .ToListAsync();

            var guests = await _context.SessionWalkinGuests
                .Where(g => g.SessionId == sessionId)
                .Include(g => g.SkillLevel)
                .OrderBy(g => g.CreatedDate)
                .Select(g => new SessionRosterPlayerDto
                {
                    ParticipantId = g.WalkinId,
                    ParticipantType = ParticipantTypes.Guest,
                    Nickname = g.GuestName,
                    FullName = g.GuestName,
                    Gender = g.Gender == 1 ? "ชาย" :
                             g.Gender == 2 ? "หญิง" :
                             g.Gender == 3 ? "อื่นๆ" : "ไม่ระบุ",
                    SkillLevelId = g.SkillLevelId,
                    SkillLevelName = g.SkillLevel != null ? g.SkillLevel.LevelName : null,
                    SkillLevelColor = g.SkillLevel != null ? g.SkillLevel.ColorHexCode : null,
                    IsCheckedIn = g.CheckinTime != null,
                    Status = (byte)(g.CheckoutTime != null ? (g.Status ?? 1) + 10 : (g.Status ?? 1)) // NEW: Map status & Checkout
                })
                .ToListAsync();

            var allPlayers = members.Cast<object>().Concat(guests.Cast<object>()).ToList();

            var roster = allPlayers.Select((player, index) =>
            {
                var rosterPlayer = (SessionRosterPlayerDto)player;
                rosterPlayer.No = index + 1;
                return rosterPlayer;
            }).ToList();


            return roster;
        }

        public async Task<bool> UpdateSessionCourtsAsync(int sessionId, int organizerUserId, UpdateCourtsDto dto)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId)
            {
                return false;
            }

            // หาสนามที่ถูกลบไป (มีใน DB เดิม แต่ไม่มีในอัปเดตใหม่)
            var oldCourts = session.CourtNumbers?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToList() ?? new List<string>();
            var newCourts = dto.CourtIdentifiers ?? new List<string>();
            var deletedCourts = oldCourts.Except(newCourts).ToList();

            // ลบแมตช์ที่จัดเตรียมไว้ (Staged: Status = 4) บนสนามที่ถูกลบทิ้งไป
            if (deletedCourts.Any())
            {
                var orphanedMatches = await _context.Matches
                    .Where(m => m.SessionId == sessionId && m.Status == 4 && m.CourtNumber != null && deletedCourts.Contains(m.CourtNumber))
                    .Include(m => m.MatchPlayers)
                    .ToListAsync();
                foreach (var match in orphanedMatches)
                {
                    _context.MatchPlayers.RemoveRange(match.MatchPlayers); // เอาผู้เล่นออกเพื่อให้กลับสู่ Waiting List เมื่อดึง Live State
                }
                _context.Matches.RemoveRange(orphanedMatches); // ลบแมตช์ทิ้ง
            }

            session.CourtNumbers = string.Join(",", newCourts);
            session.NumberOfCourts = newCourts.Count;
            await _context.SaveChangesAsync();

            // Broadcast state change
            await BroadcastLiveStateChange(sessionId, organizerUserId);

            return true;
        }
    }
}
