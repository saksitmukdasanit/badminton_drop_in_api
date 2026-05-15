using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DropInBadAPI.Services
{
    public class PlayerDashboardService : IPlayerDashboardService
    {
        private readonly BadmintonDbContext _context;

        public PlayerDashboardService(BadmintonDbContext context)
        {
            _context = context;
        }

        public async Task<PlayerDashboardDto?> GetPlayerDashboardAsync(int userId)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null) return null;

            var organizerSkills = await _context.UserOrganizerSkills
                .Include(uos => uos.SkillLevel)
                .Include(uos => uos.OrganizerUser).ThenInclude(u => u.UserProfile)
                .Where(uos => uos.UserId == userId)
                .ToListAsync();

            if (profile.SkillDisplayOrganizerUserId.HasValue
                && !organizerSkills.Any(s => s.OrganizerUserId == profile.SkillDisplayOrganizerUserId.Value))
            {
                profile.SkillDisplayOrganizerUserId = null;
                await _context.SaveChangesAsync();
            }

            UserOrganizerSkill? displaySkill = null;
            if (profile.SkillDisplayOrganizerUserId.HasValue)
            {
                displaySkill = organizerSkills.FirstOrDefault(s => s.OrganizerUserId == profile.SkillDisplayOrganizerUserId.Value);
            }
            displaySkill ??= organizerSkills
                .OrderByDescending(s => s.UpdatedDate ?? DateTime.MinValue)
                .FirstOrDefault();

            string latestSkillText = displaySkill != null
                ? $"{displaySkill.SkillLevel.LevelName} (ประเมินโดย {displaySkill.OrganizerUser?.UserProfile?.Nickname})"
                : "ยังไม่มีข้อมูลระดับมือ";

            bool usesManualPreference = profile.SkillDisplayOrganizerUserId.HasValue;

            // 2. สถิติการเล่น
            var finishedMatches = await _context.MatchPlayers
                .Include(mp => mp.Match)
                .Where(mp => mp.UserId == userId && mp.Match.Status == 2 && mp.Match.StartTime.HasValue && mp.Match.EndTime.HasValue)
                .ToListAsync();

            int totalMatches = finishedMatches.Count;
            int totalMinutes = finishedMatches.Sum(mp => (int)(mp.Match.EndTime!.Value - mp.Match.StartTime!.Value).TotalMinutes);
            
            // --- NEW: หาสถิติเพิ่มเติม ---
            int totalWins = finishedMatches.Count(mp => mp.Result == 1);

            decimal unpaidBalance = await _context.ParticipantBills
                .Where(b => b.UserId == userId && b.Status == 1) // 1 = ค้างชำระ
                .SumAsync(b => b.TotalAmount);

            decimal walletBalance = await _context.UserWallets
                .Where(w => w.UserId == userId)
                .Select(w => w.Balance)
                .FirstOrDefaultAsync();

            // 3. สถิติการใช้จ่ายรวม (เอาเฉพาะบิลที่จ่ายแล้ว)
            decimal totalSpent = await _context.ParticipantBills
                .Where(b => b.UserId == userId && b.Status == 2)
                .SumAsync(b => b.TotalAmount);

            // 4. สถิติการยกเลิก (Status = 3)
            int cancelCount = await _context.SessionParticipants
                .Where(sp => sp.UserId == userId && sp.Status == 3)
                .CountAsync();

            // 5. ผู้จัดที่ติดตามอยู่
            int followingCount = await _context.UserFollows
                .Where(f => f.FollowerId == userId)
                .CountAsync();

            // 6. ดึงก๊วนที่ใกล้จะถึงที่สุด 1 ก๊วน (Next Upcoming)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var nextSession = await _context.GameSessions
                .Where(s => s.SessionParticipants.Any(p => p.UserId == userId && p.Status == 1)) // เป็นตัวจริงเท่านั้น
                .Where(s => s.SessionDate >= today && s.Status == 1) // ยังไม่จบ
                .Include(s => s.Venue)
                .Include(s => s.CreatedByUser).ThenInclude(u => u.UserProfile)
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.GameType)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand)
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .FirstOrDefaultAsync();

            UpcomingSessionCardDto? nextSessionDto = null;
            if (nextSession != null)
            {
                var thaiCulture = new CultureInfo("th-TH");
                nextSessionDto = new UpcomingSessionCardDto
                {
                    SessionPublicId = nextSession.SessionPublicId,
                    SessionId = nextSession.SessionId,
                    GroupName = nextSession.GroupName,
                    ImageUrl = nextSession.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).FirstOrDefault(),
                    DayOfWeek = nextSession.SessionDate.ToDateTime(TimeOnly.MinValue).ToString("dddd", thaiCulture),
                    SessionDate = nextSession.SessionDate.ToString("dd/MM/yyyy", thaiCulture),
                    StartTime = nextSession.StartTime.ToString("HH:mm"),
                    EndTime = nextSession.EndTime.ToString("HH:mm"),
                    SessionStart = nextSession.SessionDate.ToDateTime(nextSession.StartTime),
                    CourtName = nextSession.Venue?.VenueName ?? "",
                    Location = nextSession.Venue?.Address ?? "-",
                    Latitude = nextSession.Venue?.Latitude,
                    Longitude = nextSession.Venue?.Longitude,
                    Price = $"{(nextSession.CourtFeePerPerson ?? 0) + (nextSession.ShuttlecockFeePerPerson ?? 0):N0} บาท",
                    OrganizerName = nextSession.CreatedByUser?.UserProfile?.Nickname ?? "N/A",
                    OrganizerImageUrl = nextSession.CreatedByUser?.UserProfile?.ProfilePhotoUrl,
                    MaxParticipants = nextSession.MaxParticipants,
                    CurrentParticipants = nextSession.SessionParticipants.Count(p => p.Status == 1 || p.Status == null) + nextSession.SessionWalkinGuests.Count(g => g.Status == 1 || g.Status == null),
                    GameTypeName = nextSession.GameType?.TypeName,
                    ShuttlecockBrandName = nextSession.ShuttlecockModel?.Brand?.BrandName,
                    ShuttlecockModelName = nextSession.ShuttlecockModel?.ModelName,
                    UserStatus = "Joined" // ถือว่าเป็น Joined แน่นอนเพราะกรอง status == 1 มา
                };
            }

            return new PlayerDashboardDto
            {
                Profile = new PlayerDashboardProfileDto
                {
                    Nickname = profile.Nickname ?? "",
                    ProfilePhotoUrl = profile.ProfilePhotoUrl,
                    LatestSkillLevelName = latestSkillText,
                    SkillDisplayOrganizerUserId = profile.SkillDisplayOrganizerUserId,
                    SkillLevelUsesManualOrganizerPreference = usesManualPreference
                },
                Stats = new PlayerDashboardStatsDto
                {
                    TotalMatches = totalMatches,
                    TotalPlayTimeMinutes = totalMinutes,
                    TotalSpent = totalSpent,
                    CancelCount = cancelCount,
                    FollowingCount = followingCount,
                    TotalWins = totalWins,
                    UnpaidBalance = unpaidBalance,
                    WalletBalance = walletBalance
                },
                NextUpcomingSession = nextSessionDto
            };
        }

        public async Task<IReadOnlyList<PlayerOrganizerSkillItemDto>> GetPlayerOrganizerSkillsAsync(int userId)
        {
            var profile = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null) return Array.Empty<PlayerOrganizerSkillItemDto>();

            var rows = await _context.UserOrganizerSkills
                .AsNoTracking()
                .Include(uos => uos.SkillLevel)
                .Include(uos => uos.OrganizerUser).ThenInclude(u => u.UserProfile)
                .Where(uos => uos.UserId == userId)
                .OrderByDescending(uos => uos.UpdatedDate ?? DateTime.MinValue)
                .ToListAsync();

            int? pref = profile.SkillDisplayOrganizerUserId;
            return rows.ConvertAll(r => new PlayerOrganizerSkillItemDto
            {
                OrganizerUserId = r.OrganizerUserId,
                OrganizerNickname = r.OrganizerUser?.UserProfile?.Nickname ?? "—",
                OrganizerProfilePhotoUrl = r.OrganizerUser?.UserProfile?.ProfilePhotoUrl,
                SkillLevelId = r.SkillLevelId,
                SkillLevelName = r.SkillLevel.LevelName,
                UpdatedDateUtc = r.UpdatedDate,
                IsPreferredForHome = pref.HasValue && pref.Value == r.OrganizerUserId
            });
        }

        public async Task<(bool ok, string? errorMessage)> SetSkillDisplayOrganizerPreferenceAsync(int userId, int? organizerUserId)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null) return (false, "ไม่พบโปรไฟล์ผู้เล่น");

            if (!organizerUserId.HasValue)
            {
                profile.SkillDisplayOrganizerUserId = null;
                await _context.SaveChangesAsync();
                return (true, null);
            }

            var exists = await _context.UserOrganizerSkills
                .AnyAsync(u => u.UserId == userId && u.OrganizerUserId == organizerUserId.Value);
            if (!exists) return (false, "ไม่มีระดับมือจากผู้จัดนี้");

            profile.SkillDisplayOrganizerUserId = organizerUserId.Value;
            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
}