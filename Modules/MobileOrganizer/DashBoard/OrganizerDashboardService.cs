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
    public class OrganizerDashboardService : IOrganizerDashboardService
    {
        private readonly BadmintonDbContext _context;

        public OrganizerDashboardService(BadmintonDbContext context)
        {
            _context = context;
        }

        public async Task<OrganizerDashboardDto?> GetOrganizerDashboardAsync(int userId)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            var organizerProfile = await _context.OrganizerProfiles.FirstOrDefaultAsync(op => op.UserId == userId);

            if (profile == null || organizerProfile == null) return null;

            // 1. สถิติการจัดก๊วนทั้งหมด (ไม่นับที่ถูกยกเลิก)
            var allSessions = await _context.GameSessions
                .Where(s => s.CreatedByUserId == userId && s.Status != 3)
                .ToListAsync();
            
            int totalSessionsHosted = allSessions.Count;

            // 2. จำนวนผู้เล่นที่เคยเข้าร่วม (Member + Walk-in)
            var totalPlayersJoined = await _context.SessionParticipants
                .CountAsync(sp => sp.Session.CreatedByUserId == userId && sp.Status == 1);
            var totalWalkinsJoined = await _context.SessionWalkinGuests
                .CountAsync(g => g.Session.CreatedByUserId == userId && g.Status == 1);

            // 3. รายได้รวมสุทธิ (หักค่าธรรมเนียมแอปแล้ว เฉพาะบิลที่จ่ายสำเร็จ Status = 2)
            var allBills = await _context.ParticipantBills
                .Include(b => b.BillLineItems)
                .Where(b => b.Session.CreatedByUserId == userId && b.Status == 2)
                .ToListAsync();

            decimal totalIncome = allBills.Sum(b => b.TotalAmount) 
                - allBills.SelectMany(b => b.BillLineItems).Where(li => li.Description == "ค่าธรรมเนียม").Sum(li => li.Amount);

            // --- NEW: หายอดเงินในกระเป๋า (Wallet) และยอดที่รอเก็บเงิน (Pending) ---
            decimal walletBalance = await _context.UserWallets
                .Where(w => w.UserId == userId)
                .Select(w => w.Balance)
                .FirstOrDefaultAsync();

            decimal pendingIncome = await _context.ParticipantBills
                .Where(b => b.Session.CreatedByUserId == userId && b.Status == 1) // 1 = ค้างชำระ
                .SumAsync(b => b.TotalAmount);

            // 4. จำนวนผู้ติดตาม
            int followersCount = await _context.UserFollows
                .CountAsync(f => f.OrganizerId == userId);

            // 5. ก๊วนถัดไปที่ใกล้จะถึง (Next Upcoming Session)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var nextSession = await _context.GameSessions
                .Where(s => s.CreatedByUserId == userId && s.SessionDate >= today && (s.Status == 1 || s.Status == 2))
                .Include(s => s.Venue)
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .Include(s => s.GameType)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand)
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
                    Location = nextSession.Venue?.Address,
                    Latitude = nextSession.Venue?.Latitude,
                    Longitude = nextSession.Venue?.Longitude,
                    Price = (nextSession.CourtFeePerPerson.HasValue || nextSession.ShuttlecockFeePerPerson.HasValue)
                          ? $"{(nextSession.CourtFeePerPerson ?? 0) + (nextSession.ShuttlecockFeePerPerson ?? 0):N0} บาท" : "สอบถามผู้จัด",
                    OrganizerName = profile.Nickname ?? "",
                    OrganizerImageUrl = profile.ProfilePhotoUrl,
                    IsBookmarked = false,
                    CurrentParticipants = nextSession.SessionParticipants.Count(p => p.Status == 1) + nextSession.SessionWalkinGuests.Count(g => g.Status == 1),
                    MaxParticipants = nextSession.MaxParticipants,
                    GameTypeName = nextSession.GameType?.TypeName,
                    ShuttlecockBrandName = nextSession.ShuttlecockModel?.Brand?.BrandName,
                    ShuttlecockModelName = nextSession.ShuttlecockModel?.ModelName,
                    Status = nextSession.Status,
                    CourtNumbers = nextSession.CourtNumbers,
                    Notes = nextSession.Notes,
                    CourtFeePerPerson = nextSession.CourtFeePerPerson?.ToString(),
                    ShuttlecockFeePerPerson = nextSession.ShuttlecockFeePerPerson?.ToString(),
                    CostingMethod = nextSession.CostingMethod,
                    UserStatus = "Organizer" // ตัวเองเป็นผู้จัด
                };
            }

            return new OrganizerDashboardDto
            {
                Profile = new OrganizerDashboardProfileDto { Nickname = profile.Nickname ?? "", ProfilePhotoUrl = profile.ProfilePhotoUrl, Status = (byte)organizerProfile.Status },
                Stats = new OrganizerDashboardStatsDto 
                { 
                    TotalSessionsHosted = totalSessionsHosted, 
                    TotalPlayersJoined = totalPlayersJoined + totalWalkinsJoined, 
                    TotalNetIncome = totalIncome < 0 ? 0 : totalIncome, 
                    WalletBalance = walletBalance,
                    TotalPendingIncome = pendingIncome,
                    FollowersCount = followersCount 
                },
                NextUpcomingSession = nextSessionDto
            };
        }
    }
}