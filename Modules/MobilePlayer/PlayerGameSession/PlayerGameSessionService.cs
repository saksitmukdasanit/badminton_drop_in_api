using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Hubs;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace DropInBadAPI.Service.MobilePlayer.Game
{
    public class PlayerGameSessionService : IPlayerGameSessionService
    {
        private readonly BadmintonDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<ManagementGameHub> _hubContext;
        private readonly IMatchManagementService _matchManagementService;
        private readonly INotificationService _notificationService;

        public PlayerGameSessionService(
            BadmintonDbContext context, 
            IConfiguration configuration,
            IHubContext<ManagementGameHub> hubContext,
            IMatchManagementService matchManagementService,
            INotificationService notificationService)
        {
            _context = context;
            _configuration = configuration;
            _hubContext = hubContext;
            _matchManagementService = matchManagementService;
            _notificationService = notificationService;
        }

        public async Task<IEnumerable<UpcomingSessionCardDto>> GetUpcomingSessionsAsync(int? currentUserId, string? keyword = null, string? sortBy = null, int? organizerId = null, List<DayOfWeek>? daysOfWeek = null, List<int>? gameTypeIds = null, int page = 1, int limit = 10)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var thaiCulture = new CultureInfo("th-TH");
            var userBookmarks = new HashSet<int>();

            if (currentUserId.HasValue)
            {
                userBookmarks = await _context.UserBookmarkedSessions
                    .Where(b => b.UserId == currentUserId.Value)
                    .Select(b => b.SessionId)
                    .ToHashSetAsync();
            }

            var query = _context.GameSessions
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .Where(s => s.SessionDate >= today && s.Status == 1);

            // กรองก๊วนที่ผู้เล่นเข้าร่วมแล้ว (ตัวจริง หรือ ตัวสำรอง) ออกจากการค้นหาเพื่อไม่ให้สับสน
            if (currentUserId.HasValue)
            {
                query = query.Where(s => !s.SessionParticipants.Any(p => p.UserId == currentUserId.Value && (p.Status == 1 || p.Status == 2)));
            }

            // --- NEW: กรองตามผู้จัด ---
            if (organizerId.HasValue)
            {
                query = query.Where(s => s.CreatedByUserId == organizerId.Value);
            }

            // 1. กรองข้อมูล (Search) จากชื่อก๊วน หรือ ชื่อสนาม ด้วย DB โดยตรง
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lowerKeyword = keyword.ToLower();
                query = query.Where(s => s.GroupName.ToLower().Contains(lowerKeyword) ||
                                         (s.Venue != null && s.Venue.VenueName.ToLower().Contains(lowerKeyword)));
            }

            // --- NEW: กรองตามวันในสัปดาห์ ---
            if (daysOfWeek != null && daysOfWeek.Any())
            {
                query = query.Where(s => daysOfWeek.Contains(s.SessionDate.DayOfWeek));
            }

            // --- NEW: กรองตามประเภทเกม ---
            if (gameTypeIds != null && gameTypeIds.Any())
            {
                query = query.Where(s => s.GameTypeId.HasValue && gameTypeIds.Contains(s.GameTypeId.Value));
            }

            // --- NEW: 2. จัดเรียงข้อมูล (Sort) ในระดับ Database ก่อนดึงข้อมูล ---
            if (sortBy == "ค่าสนาม")
            {
                query = query.OrderByDescending(s => userBookmarks.Contains(s.SessionId))
                             .ThenBy(s => (s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0));
            }
            else
            {
                // ค่าเริ่มต้น (เรียงตาม Bookmark ก่อน แล้วค่อยวันและเวลาที่เร็วที่สุดขึ้นก่อน)
                query = query.OrderByDescending(s => userBookmarks.Contains(s.SessionId))
                             .ThenBy(s => s.SessionDate)
                             .ThenBy(s => s.StartTime);
            }

            // --- NEW: 3. แบ่งหน้า (Pagination) สั่งให้ Database ส่งมาแค่ Limit ที่กำหนด ---
            query = query.Skip((page - 1) * limit).Take(limit);

            // 4. ดึงข้อมูลดิบ (Raw Data) จะดึงข้อมูลมาแค่จำนวนหน้า (เช่น 10 แถว) เท่านั้น ทำให้เบามาก!
            var rawData = await query.Select(s => new
                {
                    SessionId = s.SessionId,
                    GroupName = s.GroupName,
                    ImageUrl = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).FirstOrDefault(),
                    SessionDate = s.SessionDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    CourtName = s.Venue != null ? s.Venue.VenueName : null,
                    Location = s.Venue != null ? s.Venue.Address : null,
                    CourtFeePerPerson = s.CourtFeePerPerson,
                    ShuttlecockFeePerPerson = s.ShuttlecockFeePerPerson,
                    OrganizerName = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.Nickname : "N/A",
                    OrganizerImageUrl = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.ProfilePhotoUrl : null,
                    IsBookmarked = userBookmarks.Contains(s.SessionId),
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1 || p.Status == null) + s.SessionWalkinGuests.Count(g => g.Status == 1 || g.Status == null),
                    GameTypeName = s.GameType != null ? s.GameType.TypeName : null,
                    ShuttlecockBrandName = s.ShuttlecockModel != null && s.ShuttlecockModel.Brand != null ? s.ShuttlecockModel.Brand.BrandName : null,
                    ShuttlecockModelName = s.ShuttlecockModel != null ? s.ShuttlecockModel.ModelName : null,
                    CourtImageUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
                    Status = s.Status,
                    CourtNumbers = s.CourtNumbers,
                    Notes = s.Notes,
                    CostingMethod = s.CostingMethod,
                    UserStatus = currentUserId.HasValue
                        ? s.SessionParticipants
                            .Where(p => p.UserId == currentUserId.Value)
                            // For the upcoming list, a cancelled status (3) should be treated as 'NotJoined'
                            // so the user can book again. The 'Refund' status is handled in the 'MyGames' list.
                            .Select(p => p.Status == 1 ? (p.CheckinTime != null ? "CheckedIn" : "Joined") 
                                      : p.Status == 2 ? "Waitlisted" 
                                      : "NotJoined")
                            .FirstOrDefault() ?? "NotJoined"
                        : "NotJoined"
                }).ToListAsync();

            // 5. นำข้อมูลที่ดึงมาแล้ว (เพียง 10 แถว) มาแปลง Format (ToString)
            var result = rawData.Select(s => new UpcomingSessionCardDto
                {
                    SessionId = s.SessionId,
                    GroupName = s.GroupName,
                    ImageUrl = s.ImageUrl,
                    DayOfWeek = s.SessionDate.ToDateTime(TimeOnly.MinValue).ToString("dddd", thaiCulture),
                    SessionDate = s.SessionDate.ToString("dd/MM/yyyy", thaiCulture),
                    StartTime = s.StartTime.ToString("HH:mm"),
                    EndTime = s.EndTime.ToString("HH:mm"),
                    SessionStart = s.SessionDate.ToDateTime(s.StartTime),
                    CourtName = s.CourtName,
                    Location = s.Location,
                    Price = (s.CourtFeePerPerson.HasValue || s.ShuttlecockFeePerPerson.HasValue)
                          ? $"{(s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0):N0} บาท" : "สอบถามผู้จัด",
                    OrganizerName = s.OrganizerName,
                    OrganizerImageUrl = s.OrganizerImageUrl,
                    IsBookmarked = s.IsBookmarked,
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.CurrentParticipants,
                    GameTypeName = s.GameTypeName,
                    ShuttlecockBrandName = s.ShuttlecockBrandName,
                    ShuttlecockModelName = s.ShuttlecockModelName,
                    CourtImageUrls = s.CourtImageUrls,
                    Status = s.Status,
                    CourtNumbers = s.CourtNumbers,
                    Notes = s.Notes,
                    CourtFeePerPerson = s.CourtFeePerPerson?.ToString(),
                    ShuttlecockFeePerPerson = s.ShuttlecockFeePerPerson?.ToString(),
                    CostingMethod = s.CostingMethod,
                    UserStatus = s.UserStatus
                }).ToList();

            return result;
        }

        public async Task<IEnumerable<UpcomingSessionCardDto>> GetBookmarkedSessionsAsync(int userId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var thaiCulture = new CultureInfo("th-TH");

            var bookmarkedSessionIds = await _context.UserBookmarkedSessions
                .Where(b => b.UserId == userId)
                .Select(b => b.SessionId)
                .ToListAsync();

            var sessions = await _context.GameSessions
                // กรองเฉพาะ Session ที่ถูกบุ๊กมาร์ก และ วันที่ >= วันนี้ (ไม่เอาวันย้อนหลัง) และ สถานะเปิดรับอยู่
                .Where(s => bookmarkedSessionIds.Contains(s.SessionId) && s.SessionDate >= today && s.Status == 1)
                .Include(s => s.Venue)
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.CreatedByUser).ThenInclude(u => u.UserProfile)
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .Include(s => s.GameType)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .ToListAsync();

            return sessions.Select(s => new UpcomingSessionCardDto
            {
                SessionId = s.SessionId,
                GroupName = s.GroupName,
                ImageUrl = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).FirstOrDefault(),
                DayOfWeek = s.SessionDate.ToDateTime(TimeOnly.MinValue).ToString("dddd", thaiCulture),
                SessionDate = s.SessionDate.ToString("dd/MM/yyyy", thaiCulture),
                StartTime = s.StartTime.ToString("HH:mm"),
                EndTime = s.EndTime.ToString("HH:mm"),
                SessionStart = s.SessionDate.ToDateTime(s.StartTime),
                CourtName = s.Venue != null ? s.Venue.VenueName : null,
                Location = s.Venue != null ? s.Venue.Address : null,
                Price = (s.CourtFeePerPerson.HasValue || s.ShuttlecockFeePerPerson.HasValue) ? $"{(s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0):N0} บาท" : "สอบถามผู้จัด",
                OrganizerName = s.CreatedByUser?.UserProfile?.Nickname ?? "N/A",
                OrganizerImageUrl = s.CreatedByUser?.UserProfile?.ProfilePhotoUrl,
                IsBookmarked = true,
                MaxParticipants = s.MaxParticipants,
                CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1 || p.Status == null) + s.SessionWalkinGuests.Count(g => g.Status == 1 || g.Status == null),
                GameTypeName = s.GameType?.TypeName,
                ShuttlecockBrandName = s.ShuttlecockModel?.Brand?.BrandName,
                ShuttlecockModelName = s.ShuttlecockModel?.ModelName,
                CourtImageUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
                Status = s.Status,
                CourtNumbers = s.CourtNumbers,
                Notes = s.Notes,
                CourtFeePerPerson = s.CourtFeePerPerson.ToString(),
                ShuttlecockFeePerPerson = s.ShuttlecockFeePerPerson.ToString(),
                CostingMethod = s.CostingMethod,
                UserStatus = s.SessionParticipants.Where(p => p.UserId == userId).Select(p => p.Status == 1 ? (p.CheckinTime != null ? "CheckedIn" : "Joined") : p.Status == 2 ? "Waitlisted" : p.Status == 3 ? "Refund" : "NotJoined").FirstOrDefault() ?? "NotJoined"
            }).ToList();
        }

        public async Task<MyGameSessionsResponseDto> GetMySessionsAsync(int userId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var thaiCulture = new CultureInfo("th-TH");
            var userBookmarks = await _context.UserBookmarkedSessions
                .Where(b => b.UserId == userId)
                .Select(b => b.SessionId)
                .ToHashSetAsync();

            // ดึงข้อมูลก๊วนทั้งหมดที่มีชื่อ User คนนี้อยู่ใน Participant List
            var sessions = await _context.GameSessions
                .Where(s => s.SessionParticipants.Any(p => p.UserId == userId))
                // ซ่อนก๊วนที่ผู้เล่นคนนี้ Checkout (จ่ายเงินและออกไปแล้ว) เพื่อให้ย้ายไปโผล่ที่หน้าประวัติแทน
                .Where(s => !s.SessionParticipants.Any(p => p.UserId == userId && p.CheckoutTime != null))
                // กรองเฉพาะเกมที่ยังไม่ผ่านไป หรือ เกมที่สถานะถูกยกเลิก/รอคืนเงิน (เพื่อไม่ให้ประวัติหายก่อนได้เงินคืน)
                .Where(s => s.SessionDate >= today ||
                            s.Status == 3 || s.Status == 4 || 
                            s.SessionParticipants.Any(p => p.UserId == userId && p.Status == 3))
                .Include(s => s.Venue)
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.CreatedByUser.UserProfile)
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests) // FIX: Include Walk-in ให้ครบ
                .Include(s => s.GameType)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand)
                .Include(s => s.ParticipantBills) // NEW: Include Bills เพื่อเช็คค้างชำระ
                .Include(s => s.Matches).ThenInclude(m => m.MatchPlayers) // NEW: Include Matches เพื่อนับเกม
                .OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.StartTime)
                .ToListAsync();

            decimal serviceFee = _configuration.GetValue<decimal>("ServiceFee");
            var dtos = sessions.Select(s => {
                // หาสถานะเฉพาะของ User คนนี้ในก๊วนนั้นๆ
                var userParticipation = s.SessionParticipants.FirstOrDefault(p => p.UserId == userId);
                string userStatusStr = userParticipation?.Status switch
                {
                    1 => userParticipation.CheckoutTime != null ? "CheckedOut" : (userParticipation.CheckinTime != null ? "CheckedIn" : "Joined"), // เพิ่มการเช็ค CheckedOut
                    2 => "Waitlisted",  // สำรอง
                    3 => "Refund",      // ยกเลิก / รอคืนเงิน
                    _ => "NotJoined"
                };
                
                // --- FIX: คำนวณยอดที่ต้องจ่ายจริงเทียบกับยอดที่จ่ายแล้ว ---
                int matchesPlayed = s.Matches.Count(m => m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == userId));
                
                decimal expectedTotal = 0;
                bool isUnpaid = false;

                if (userParticipation != null && userParticipation.Status != 3) // ไม่รวมคนที่ยกเลิกไปแล้ว
                {
                    var validBills = s.ParticipantBills.Where(b => b.UserId == userId && b.Status != 3).ToList();
                    decimal paidAmount = validBills.Where(b => b.Status == 2).Sum(b => b.TotalAmount);
                    
                    decimal customItems = validBills.SelectMany(b => b.BillLineItems)
                        .Where(li => li.Description != "ค่าสนาม" && li.Description != "ค่าธรรมเนียม" && !li.Description.StartsWith("ค่าลูกแบด"))
                        .Sum(li => li.Amount);

                    expectedTotal = (s.CourtFeePerPerson ?? 0) + serviceFee + customItems;
                    if (s.CostingMethod == 2) expectedTotal += (s.ShuttlecockFeePerPerson ?? 0);
                    else expectedTotal += (s.ShuttlecockFeePerPerson ?? 0) * matchesPlayed;

                    decimal billedTotal = validBills.Sum(b => b.TotalAmount);
                    if (billedTotal > expectedTotal) expectedTotal = billedTotal;

                    isUnpaid = s.ParticipantBills.Any(b => b.UserId == userId && b.Status == 1) || (expectedTotal - paidAmount > 0.1m);
                }

                return new UpcomingSessionCardDto
                {
                    SessionId = s.SessionId,
                    GroupName = s.GroupName,
                    ImageUrl = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).FirstOrDefault(),
                    DayOfWeek = s.SessionDate.ToDateTime(TimeOnly.MinValue).ToString("dddd", thaiCulture),
                    SessionDate = s.SessionDate.ToString("dd/MM/yyyy", thaiCulture),
                    StartTime = s.StartTime.ToString("HH:mm"),
                    EndTime = s.EndTime.ToString("HH:mm"),
                    SessionStart = s.SessionDate.ToDateTime(s.StartTime),
                    CourtName = s.Venue != null ? s.Venue.VenueName : null,
                    Location = s.Venue != null ? s.Venue.Address : null,
                    // ส่งยอดจ่ายจริงกลับไปแสดง
                    Price = expectedTotal > 0 ? $"{expectedTotal:N0} บาท" : "สอบถามผู้จัด",
                    OrganizerName = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.Nickname : "N/A",
                    OrganizerImageUrl = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.ProfilePhotoUrl : null,
                    IsBookmarked = userBookmarks.Contains(s.SessionId),
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1 || p.Status == null) + s.SessionWalkinGuests.Count(g => g.Status == 1 || g.Status == null),
                    GameTypeName = s.GameType != null ? s.GameType.TypeName : null,
                    ShuttlecockBrandName = s.ShuttlecockModel != null && s.ShuttlecockModel.Brand != null ? s.ShuttlecockModel.Brand.BrandName : null,
                    ShuttlecockModelName = s.ShuttlecockModel != null ? s.ShuttlecockModel.ModelName : null,
                    CourtImageUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
                    Status = s.Status,
                    CourtNumbers = s.CourtNumbers,
                    Notes = s.Notes,
                    CourtFeePerPerson = s.CourtFeePerPerson.ToString(),
                    ShuttlecockFeePerPerson = s.ShuttlecockFeePerPerson.ToString(),
                    CostingMethod = s.CostingMethod,
                    UserStatus = isUnpaid ? "PendingPayment" : userStatusStr
                };
            }).ToList();

            return new MyGameSessionsResponseDto
            {
                Playing = dtos.Where(d => d.Status == 2 || d.Status == 6).ToList(),
                Refund = dtos.Where(d => d.UserStatus == "Refund" || d.Status == 3 || d.Status == 4).ToList(),
                Upcoming = dtos.Where(d => !(d.Status == 2 || d.Status == 6) && !(d.UserStatus == "Refund" || d.Status == 3 || d.Status == 4)).ToList()
            };
        }

        public async Task<IEnumerable<UpcomingSessionCardDto>> GetHistorySessionsAsync(int userId, string? keyword = null, string? sortBy = null, int page = 1, int limit = 10)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var thaiCulture = new CultureInfo("th-TH");
            var userBookmarks = await _context.UserBookmarkedSessions
                .Where(b => b.UserId == userId)
                .Select(b => b.SessionId)
                .ToHashSetAsync();

            // 1. ดึงเฉพาะก๊วนที่ผู้เล่นคนนี้มีชื่ออยู่ และ "วันที่ผ่านไปแล้ว"
            IQueryable<GameSession> query = _context.GameSessions
                .Where(s => s.SessionParticipants.Any(p => p.UserId == userId))
                // ดึงมาแสดงถ้า 1) วันที่ผ่านไปแล้ว 2) ก๊วนนั้นจบแล้ว(Status=4) หรือ 3) ตัวผู้เล่นเอง Checkout/จ่ายเงินไปแล้ว
                .Where(s => s.SessionDate < today || s.Status == 4 || s.SessionParticipants.Any(p => p.UserId == userId && p.CheckoutTime != null))
                .Include(s => s.Venue)
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.CreatedByUser).ThenInclude(u => u.UserProfile)
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .Include(s => s.GameType)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand)
                .Include(s => s.ParticipantBills)
                .Include(s => s.Matches).ThenInclude(m => m.MatchPlayers);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lowerKeyword = keyword.ToLower();
                query = query.Where(s => s.GroupName.ToLower().Contains(lowerKeyword) ||
                                         (s.Venue != null && s.Venue.VenueName.ToLower().Contains(lowerKeyword)));
            }

            // จัดเรียงและตัดหน้าใน DB (ใช้ AsSplitQuery เพื่อป้องกัน Cartesian Explosion Error ที่ทำให้ List ว่าง)
            query = query.AsSplitQuery().OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.StartTime);
            query = query.Skip((page - 1) * limit).Take(limit);

            // ดึงเข้า Memory เพื่อคำนวณยอดเงินที่แม่นยำ
            var rawSessions = await query.ToListAsync();
            decimal serviceFee = _configuration.GetValue<decimal>("ServiceFee");

            var result = rawSessions.Select(s => 
            {
                var participant = s.SessionParticipants.FirstOrDefault(p => p.UserId == userId);
                int matchesPlayed = s.Matches.Count(m => m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == userId));
                
                decimal expectedTotal = 0;
                bool isUnpaid = false;

                if (participant != null && participant.Status != 3) // ไม่รวมคนที่ยกเลิกไปแล้ว
                {
                    var validBills = s.ParticipantBills.Where(b => b.UserId == userId && b.Status != 3).ToList();
                    decimal paidAmount = validBills.Where(b => b.Status == 2).Sum(b => b.TotalAmount);
                    
                    decimal customItems = validBills.SelectMany(b => b.BillLineItems)
                        .Where(li => li.Description != "ค่าสนาม" && li.Description != "ค่าธรรมเนียม" && !li.Description.StartsWith("ค่าลูกแบด"))
                        .Sum(li => li.Amount);

                    expectedTotal = (s.CourtFeePerPerson ?? 0) + serviceFee + customItems;
                    if (s.CostingMethod == 2) expectedTotal += (s.ShuttlecockFeePerPerson ?? 0);
                    else expectedTotal += (s.ShuttlecockFeePerPerson ?? 0) * matchesPlayed;

                    decimal billedTotal = validBills.Sum(b => b.TotalAmount);
                    if (billedTotal > expectedTotal) expectedTotal = billedTotal;

                    isUnpaid = s.ParticipantBills.Any(b => b.UserId == userId && b.Status == 1) || (expectedTotal - paidAmount > 0.1m);
                }

                string userStatusStr = participant?.Status switch
                {
                    1 => participant.CheckoutTime != null ? "CheckedOut" : (participant.CheckinTime != null ? "CheckedIn" : "Joined"),
                    2 => "Waitlisted",
                    3 => "Refund",
                    _ => "NotJoined"
                };

                return new UpcomingSessionCardDto
                {
                    SessionId = s.SessionId,
                    GroupName = s.GroupName,
                    ImageUrl = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).FirstOrDefault(),
                    DayOfWeek = s.SessionDate.ToDateTime(TimeOnly.MinValue).ToString("dddd", thaiCulture),
                    SessionDate = s.SessionDate.ToString("dd/MM/yyyy", thaiCulture),
                    StartTime = s.StartTime.ToString("HH:mm"),
                    EndTime = s.EndTime.ToString("HH:mm"),
                    SessionStart = s.SessionDate.ToDateTime(s.StartTime),
                    CourtName = s.Venue != null ? s.Venue.VenueName : null,
                    Location = s.Venue != null ? s.Venue.Address : null,
                    Price = expectedTotal > 0 ? $"{expectedTotal:N0} บาท" : "สอบถามผู้จัด",
                    OrganizerName = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.Nickname : "N/A",
                    OrganizerImageUrl = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.ProfilePhotoUrl : null,
                    IsBookmarked = userBookmarks.Contains(s.SessionId),
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1 || p.Status == null) + s.SessionWalkinGuests.Count(g => g.Status == 1 || g.Status == null),
                    GameTypeName = s.GameType != null ? s.GameType.TypeName : null,
                    ShuttlecockBrandName = s.ShuttlecockModel != null && s.ShuttlecockModel.Brand != null ? s.ShuttlecockModel.Brand.BrandName : null,
                    ShuttlecockModelName = s.ShuttlecockModel != null ? s.ShuttlecockModel.ModelName : null,
                    CourtImageUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
                    Status = s.Status,
                    CourtNumbers = s.CourtNumbers,
                    Notes = s.Notes,
                    CourtFeePerPerson = s.CourtFeePerPerson.ToString(),
                    ShuttlecockFeePerPerson = s.ShuttlecockFeePerPerson.ToString(),
                    CostingMethod = s.CostingMethod,
                    UserStatus = (isUnpaid && participant?.Status != 3) ? "PendingPayment" : userStatusStr
                };
            }).ToList();

            return result;
        }

        public async Task<PlayerGameSessionViewDto?> GetSessionForPlayerViewAsync(int sessionId, int? currentUserId)
        {
            var session = await _context.GameSessions
                .Where(s => s.SessionId == sessionId)
                .Include(s => s.Venue)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m.Brand)
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.GameSessionFacilities).ThenInclude(f => f.Facility)
                .Include(s => s.CreatedByUser).ThenInclude(u => u.UserProfile)
                .Select(s => new PlayerGameSessionViewDto
                {
                    SessionId = s.SessionId,
                    GroupName = s.GroupName,
                    Status = s.Status ?? 1,
                    SessionStart = s.SessionDate.ToDateTime(s.StartTime),
                    SessionEnd = s.SessionDate.ToDateTime(s.EndTime),
                    VenueName = s.Venue != null ? s.Venue.VenueName : "N/A",
                    VenueAddress = s.Venue != null ? s.Venue.Address : "N/A",
                    Latitude = s.Venue != null ? s.Venue.Latitude : 0,
                    Longitude = s.Venue != null ? s.Venue.Longitude : 0,
                    Organizer = new OrganizerInfoDto
                    {
                        UserId = s.CreatedByUserId,
                        Nickname = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.Nickname : "N/A",
                        ProfilePhotoUrl = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.ProfilePhotoUrl : null
                    },
                    ShuttlecockInfo = s.ShuttlecockModel != null && s.ShuttlecockModel.Brand != null ? $"{s.ShuttlecockModel.Brand.BrandName} - {s.ShuttlecockModel.ModelName}" : null,
                    MaxParticipants = s.MaxParticipants,
                    Notes = s.Notes,
                    PhotoUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
                    Facilities = s.GameSessionFacilities.Select(f => new FacilityDto(f.FacilityId, f.Facility.FacilityName, f.Facility.IconUrl)).ToList()
                })
                .FirstOrDefaultAsync();

            if (session == null) return null;

            var participants = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId && p.Status != 3)
                .Select(p => new ParticipantDto
                {
                    ParticipantId = p.ParticipantId,
                    ParticipantType = "Member",
                    UserId = p.UserId,
                    Nickname = p.User != null && p.User.UserProfile != null ? p.User.UserProfile.Nickname : "N/A",
                    FullName = p.User != null && p.User.UserProfile != null ? p.User.UserProfile.FirstName + " " + p.User.UserProfile.LastName : "N/A",
                    GenderName = p.User != null && p.User.UserProfile != null && p.User.UserProfile.Gender.HasValue ? (p.User.UserProfile.Gender == 1 ? "ชาย" : p.User.UserProfile.Gender == 2 ? "หญิง" : "อื่นๆ") : null,
                    ProfilePhotoUrl = p.User != null && p.User.UserProfile != null ? p.User.UserProfile.ProfilePhotoUrl : null,
                    SkillLevelId = p.SkillLevelId,
                    SkillLevelName = p.SkillLevel != null ? p.SkillLevel.LevelName : "N/A",
                    SkillLevelColor = p.SkillLevel != null ? p.SkillLevel.ColorHexCode : "#FFFFFF",
                    Status = p.Status ?? 1,
                    CheckinTime = p.CheckinTime
                })
                .ToListAsync();

            // --- เพิ่มการดึงข้อมูล Walk-in Guests เข้ามาแสดงในรายชื่อผู้เล่นด้วย ---
            var walkinGuests = await _context.SessionWalkinGuests
                .Where(g => g.SessionId == sessionId && g.Status != 3)
                .Select(g => new ParticipantDto
                {
                    ParticipantId = g.WalkinId,
                    ParticipantType = "Guest",
                    UserId = null,
                    Nickname = g.GuestName,
                    FullName = g.GuestName,
                    GenderName = g.Gender.HasValue ? (g.Gender == 1 ? "ชาย" : g.Gender == 2 ? "หญิง" : "อื่นๆ") : null,
                    ProfilePhotoUrl = null,
                    SkillLevelId = g.SkillLevelId,
                    SkillLevelName = g.SkillLevel != null ? g.SkillLevel.LevelName : "N/A",
                    SkillLevelColor = g.SkillLevel != null ? g.SkillLevel.ColorHexCode : "#FFFFFF",
                    Status = g.Status ?? 1,
                    CheckinTime = g.CheckinTime
                })
                .ToListAsync();

            participants.AddRange(walkinGuests);
            
            session.Participants = participants;
            session.CurrentParticipants = participants.Count(p => p.Status == 1);

            session.CurrentUserStatus = "NotJoined";
            if (currentUserId.HasValue)
            {
                var currentUserParticipation = await _context.SessionParticipants
                    .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == currentUserId.Value);

                if (currentUserParticipation != null)
                {
                    session.CurrentUserStatus = currentUserParticipation.Status switch
                    {
                        1 => currentUserParticipation.CheckoutTime != null ? "CheckedOut" : (currentUserParticipation.CheckinTime != null ? "CheckedIn" : "Joined"),
                        2 => "Waitlisted",
                        3 => "Refund",
                        _ => "NotJoined"
                    };
                }
            }

            return session;
        }

        public async Task<PlayerHistoryDetailDto?> GetHistoryDetailAsync(int sessionId, int userId)
        {
            var session = await _context.GameSessions
                .Include(s => s.SessionParticipants)
                .Include(s => s.ParticipantBills).ThenInclude(b => b.BillLineItems)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return null;

            var participant = session.SessionParticipants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null) return null;

            var playedMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == userId))
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin)
                .OrderBy(m => m.StartTime)
                .ToListAsync();

            var result = new PlayerHistoryDetailDto();

            // 1. สถานะตัวจริง/ตัวสำรอง
            result.UserStatus = participant.Status switch { 1 => "Joined", 2 => "Waitlisted", 3 => "Refund", _ => "NotJoined" };

            // 2. ข้อมูลการจ่ายเงิน
            var validBills = session.ParticipantBills.Where(b => b.UserId == userId && b.Status != 3).ToList();
            decimal serviceFee = _configuration.GetValue<decimal>("ServiceFee");
            
            int matchesPlayedCount = playedMatches.Count;
            decimal expectedTotal = (session.CourtFeePerPerson ?? 0) + serviceFee;
            if (session.CostingMethod == 2) expectedTotal += (session.ShuttlecockFeePerPerson ?? 0);
            else expectedTotal += (session.ShuttlecockFeePerPerson ?? 0) * matchesPlayedCount;

            // --- NEW: รวมค่าใช้จ่ายอื่นๆ (Custom Items) ที่ผู้จัดเพิ่มเข้ามา ---
            decimal customItems = validBills.SelectMany(b => b.BillLineItems)
                .Where(li => li.Description != "ค่าสนาม" && li.Description != "ค่าธรรมเนียม" && !li.Description.StartsWith("ค่าลูกแบด"))
                .Sum(li => li.Amount);
            
            expectedTotal += customItems;

            decimal paidAmount = validBills.Where(b => b.Status == 2).Sum(b => b.TotalAmount);
            bool hasPendingBill = validBills.Any(b => b.Status == 1);
            
            decimal billedTotal = validBills.Sum(b => b.TotalAmount);
            if (billedTotal > expectedTotal) expectedTotal = billedTotal;
            
            // ถ้ายอดที่ควรจ่าย > ยอดที่จ่ายไปแล้ว ให้ถือว่าค้างชำระทันที (แม้จะยังไม่มีบิลค้าง)
            bool isUnpaid = hasPendingBill || (expectedTotal - paidAmount > 0.1m);

            if (isUnpaid)
            {
                result.Payment.Status = "Pending";
                if (hasPendingBill)
                {
                    // ถ้ามีบิลค้าง (Status 1) ให้ดึงจากบิลนั้นมาแสดง
                    var pendingBills = validBills.Where(b => b.Status == 1).ToList();
                    result.Payment.LineItems = pendingBills.SelectMany(b => b.BillLineItems)
                        .Select(li => new CustomLineItemDto { Description = li.Description, Amount = li.Amount }).ToList();
                    result.Payment.TotalAmount = pendingBills.Sum(b => b.TotalAmount);
                }
                else
                {
                    // ถ้ายังไม่มีบิลค้าง แต่ยอด expected > paid (เล่นลูกแบดเพิ่มแต่ผู้จัดยังไม่กด Checkout)
                    decimal dueAmount = expectedTotal - paidAmount;
                    result.Payment.TotalAmount = dueAmount;
                    
                    if (paidAmount == 0) 
                    {
                        // กรณีที่ยังไม่จ่ายค่าสนามเลยตั้งแต่ต้น
                        if (session.CourtFeePerPerson > 0) result.Payment.LineItems.Add(new CustomLineItemDto { Description = "ค่าสนาม", Amount = session.CourtFeePerPerson.Value });
                        if (serviceFee > 0) result.Payment.LineItems.Add(new CustomLineItemDto { Description = "ค่าธรรมเนียม", Amount = serviceFee });
                        decimal shuttleTotal = dueAmount - (session.CourtFeePerPerson ?? 0) - serviceFee;
                        if (shuttleTotal > 0) result.Payment.LineItems.Add(new CustomLineItemDto { Description = "ค่าลูกแบด", Amount = shuttleTotal });
                    }
                    else
                    {
                        // กรณีจ่ายค่าสนามไปแล้ว ค้างแค่ส่วนต่าง (ค่าลูกแบด)
                        result.Payment.LineItems.Add(new CustomLineItemDto { Description = "ค่าลูกแบด (ยังไม่ออกบิล)", Amount = dueAmount });
                    }
                }
            }
            else
            {
                // จ่ายครบแล้ว (Completed)
                result.Payment.Status = "Completed";
                result.Payment.TotalAmount = paidAmount;
                
                var paidBills = validBills.Where(b => b.Status == 2).ToList();
                result.Payment.LineItems = paidBills.SelectMany(b => b.BillLineItems)
                    .Select(li => new CustomLineItemDto { Description = li.Description, Amount = li.Amount }).ToList();
                
                // ชดเชยข้อมูลให้บิลเก่าที่อาจไม่มี LineItem ครบ
                bool hasCourtFee = result.Payment.LineItems.Any(li => li.Description == "ค่าสนาม");
                if (!hasCourtFee)
                {
                    if (serviceFee > 0) result.Payment.LineItems.Insert(0, new CustomLineItemDto { Description = "ค่าธรรมเนียม", Amount = serviceFee });
                    if (session.CourtFeePerPerson > 0) result.Payment.LineItems.Insert(0, new CustomLineItemDto { Description = "ค่าสนาม", Amount = session.CourtFeePerPerson.Value });
                }

                if (paidBills.Any())
                {
                    var latestBillId = paidBills.OrderByDescending(b => b.CreatedDate).First().BillId;
                    var payment = await _context.Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefaultAsync(p => p.BillId == latestBillId);
                    if (payment != null)
                    {
                        result.Payment.PaymentDate = payment.PaymentDate.AddHours(7).ToString("dd/MM/yy HH:mm น.");
                        result.Payment.PaymentMethod = payment.PaymentMethod == 1 ? "Cash" : "QR Code";
                    }
                }
            }

            // 3. ข้อมูลแมตช์ที่เล่นและเวลา
            int totalPlayTime = 0;
            foreach (var match in playedMatches)
            {
                var myMatchPlayer = match.MatchPlayers.First(mp => mp.UserId == userId);
                var duration = (match.StartTime.HasValue && match.EndTime.HasValue) ? (int)(match.EndTime.Value - match.StartTime.Value).TotalMinutes : 0;
                totalPlayTime += duration;

                result.Matches.Add(new HistoryMatchDto
                {
                    MatchId = match.MatchId,
                    Result = myMatchPlayer.Result,
                    Notes = myMatchPlayer.Notes,
                    DurationMinutes = duration,
                    CourtNumber = match.CourtNumber,
                    ShuttlecocksUsed = match.ShuttlecocksUsed,
                    MyTeam = match.MatchPlayers.Where(mp => mp.Team == myMatchPlayer.Team).Select(mp => new PlayerInMatchDto { 
                        UserId = mp.UserId,
                        WalkinId = mp.WalkinId,
                        Nickname = mp.UserId.HasValue ? mp.User.UserProfile.Nickname : mp.Walkin?.GuestName ?? "N/A", 
                        ProfilePhotoUrl = mp.UserId.HasValue ? mp.User.UserProfile.ProfilePhotoUrl : null 
                    }).ToList(),
                    Opponents = match.MatchPlayers.Where(mp => mp.Team != myMatchPlayer.Team).Select(mp => new PlayerInMatchDto { 
                        UserId = mp.UserId,
                        WalkinId = mp.WalkinId,
                        Nickname = mp.UserId.HasValue ? mp.User.UserProfile.Nickname : mp.Walkin?.GuestName ?? "N/A", 
                        ProfilePhotoUrl = mp.UserId.HasValue ? mp.User.UserProfile.ProfilePhotoUrl : null 
                    }).ToList()
                });
            }

            result.Summary.TotalGames = playedMatches.Count;
            result.Summary.TotalShuttlecocks = playedMatches.Sum(m => m.ShuttlecocksUsed);
            result.Summary.TotalPlayTime = totalPlayTime;

            // 4. คำนวณเวลาที่รอ (คร่าวๆ)
            int totalSessionTime = 0;
            if (participant.CheckinTime.HasValue && participant.CheckoutTime.HasValue) {
                totalSessionTime = (int)(participant.CheckoutTime.Value - participant.CheckinTime.Value).TotalMinutes;
            } else if (participant.CheckinTime.HasValue && playedMatches.Any()) {
                var lastMatchEnd = playedMatches.Max(m => m.EndTime);
                if (lastMatchEnd.HasValue && lastMatchEnd.Value > participant.CheckinTime.Value) totalSessionTime = (int)(lastMatchEnd.Value - participant.CheckinTime.Value).TotalMinutes;
            }
            int waitTime = totalSessionTime - totalPlayTime;
            result.Summary.TotalWaitTime = waitTime > 0 ? waitTime : 0;

            return result;
        }

        public async Task<(JoinSessionResponseDto? Data, string ErrorMessage)> JoinSessionAsync(int sessionId, int userId, PlayerJoinSessionRequestDto dto)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var session = await _context.GameSessions
                        .Include(s => s.SessionParticipants)
                        .Include(s => s.SessionWalkinGuests)
                        .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                    if (session == null) return (null, "Session not found.");
                    if (session.Status != 1) return (null, "This session is no longer open for booking.");
                    if (session.CreatedByUserId == userId) return (null, "Organizers cannot join their own session as a participant.");

                    var existingParticipant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.UserId == userId && p.SessionId == sessionId);
                    if (existingParticipant != null && existingParticipant.Status != 3) return (null, "You are already registered for this session.");

                    // --- Concurrency Check ---
                    var activeParticipants = session.SessionParticipants.Count(p => p.Status == 1) + session.SessionWalkinGuests.Count(g => g.Status == 1);
                    var waitlistedParticipants = session.SessionParticipants.Count(p => p.Status == 2) + session.SessionWalkinGuests.Count(g => g.Status == 2);

                    int newStatus = (activeParticipants < session.MaxParticipants && waitlistedParticipants == 0) ? 1 : 2;
                    string statusMessage = newStatus == 1 ? "Joined successfully." : "You are on the waitlist.";

                    // --- Create or Update Participant Entry ---
                    SessionParticipant participantEntry = existingParticipant ?? new SessionParticipant { SessionId = sessionId, UserId = userId };
                    participantEntry.Status = (byte)newStatus;
                    participantEntry.JoinedDate = DateTime.UtcNow;
                    // participantEntry.AutoPromote = dto.AutoPromote; // **NOTE: Add 'AutoPromote' property to SessionParticipant model**

                    int? savedSkillLevelId = await _context.UserOrganizerSkills
                        .Where(uos => uos.OrganizerUserId == session.CreatedByUserId && uos.UserId == userId)
                        .Select(uos => (int?)uos.SkillLevelId)
                        .FirstOrDefaultAsync();
                    participantEntry.SkillLevelId = savedSkillLevelId;

                    if (existingParticipant == null)
                    {
                        await _context.SessionParticipants.AddAsync(participantEntry);
                    }
                    await _context.SaveChangesAsync(); // Save to get ParticipantId

                    // --- Payment Processing ---
                    decimal courtFee = session.CourtFeePerPerson ?? 0;
                    decimal serviceFee = _configuration.GetValue<decimal>("ServiceFee");
                    decimal totalAmount = courtFee + serviceFee;

                    if (totalAmount > 0)
                    {
                        var newBill = new ParticipantBill
                        {
                            SessionId = sessionId,
                            UserId = userId,
                            CreatedDate = DateTime.UtcNow,
                            TotalAmount = totalAmount,
                            Status = 2, // Mark as Paid immediately
                            BillLineItems = new List<BillLineItem>()
                        };
                        if (courtFee > 0) newBill.BillLineItems.Add(new BillLineItem { Description = "ค่าสนาม", Amount = courtFee });
                        if (serviceFee > 0) newBill.BillLineItems.Add(new BillLineItem { Description = "ค่าธรรมเนียม", Amount = serviceFee });

                        var newPayment = new Payment
                        {
                            PaymentMethod = dto.PaymentMethod == "Credit/Debit Card" ? (byte)1 : (byte)2, // Assuming 1=Card, 2=Other digital
                            Amount = totalAmount,
                            PaymentDate = DateTime.UtcNow,
                        };
                        newBill.Payments.Add(newPayment);
                        _context.ParticipantBills.Add(newBill);
                    }

                    await _context.SaveChangesAsync();

                    // --- แจ้งเตือนผู้จัด ---
                    var user = await _context.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.UserId == userId);
                    string userName = user?.UserProfile?.Nickname ?? "ผู้เล่น";
                    string notiTitle = newStatus == 1 ? "ผู้เล่นเข้าร่วมก๊วน" : "ผู้เล่นลงชื่อสำรอง";
                    string notiMsg = $"{userName} ได้{(newStatus == 1 ? "เข้าร่วม" : "ลงคิวสำรอง")}ก๊วน {session.GroupName}";
                    await _notificationService.SendNotificationAsync(session.CreatedByUserId, notiTitle, notiMsg, "JOIN_SESSION", sessionId);

                    await transaction.CommitAsync();

                    return (new JoinSessionResponseDto { ParticipantId = participantEntry.ParticipantId, Status = newStatus, StatusMessage = statusMessage }, string.Empty);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return (null, $"An error occurred: {ex.Message}");
                }
            });
        }

        public async Task<(bool Success, string ErrorMessage)> CancelBookingAsync(int sessionId, int userId)
        {
            var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);
            if (participant == null || participant.Status == 3) return (false, "Booking not found.");
            participant.Status = 3;
            await _context.SaveChangesAsync();

            // --- แจ้งเตือนผู้จัด ---
            var session = await _context.GameSessions.FindAsync(sessionId);
            var user = await _context.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.UserId == userId);
            if (session != null && user != null)
            {
                await _notificationService.SendNotificationAsync(session.CreatedByUserId, "ผู้เล่นยกเลิกการจอง", $"{user.UserProfile?.Nickname ?? "ผู้เล่น"} ได้ยกเลิกการเข้าร่วมก๊วน {session.GroupName}", "CANCEL_BOOKING", sessionId);
            }

            return (true, "Your booking has been cancelled.");
        }

        public async Task<(bool Success, string ErrorMessage)> PlayerCheckinAsync(int sessionId, int userId, string scannedQrCode)
        {
            var session = await _context.GameSessions
                .Include(s => s.CreatedByUser)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return (false, "Session not found.");
            
            // ตรวจสอบว่า QR Code ที่สแกนมา ตรงกับ Public ID ของผู้จัดหรือไม่
            if (session.CreatedByUser.UserPublicId.ToString() != scannedQrCode)
            {
                return (false, "Invalid QR Code. Please scan the organizer's QR code.");
            }

            var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);
            if (participant == null || participant.Status != 1) return (false, "You are not an active participant in this session.");
            if (participant.CheckinTime != null) return (false, "You are already checked in.");

            participant.CheckinTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // --- แจ้งเตือนผู้จัด ---
            var user = await _context.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.UserId == userId);
            await _notificationService.SendNotificationAsync(session.CreatedByUserId, "ผู้เล่นเช็คอิน", $"{user?.UserProfile?.Nickname ?? "ผู้เล่น"} ได้เช็คอินเข้าสนามแล้ว", "PLAYER_CHECKIN", sessionId);

            return (true, "Check-in successful.");
        }

        public async Task<PlayerBillPreviewDto?> GetMyBillPreviewAsync(int sessionId, int userId)
        {
            var session = await _context.GameSessions.AsNoTracking().FirstOrDefaultAsync(s => s.SessionId == sessionId);
            if (session == null) return null;

            var participant = await _context.SessionParticipants.AsNoTracking().FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);
            if (participant == null) return null;

            var dto = new PlayerBillPreviewDto();

            // 1. Court Fee & Service Fee
            var initialBill = await _context.ParticipantBills
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.SessionId == sessionId && b.UserId == userId && b.Status != 3);

            // If the initial bill was NOT paid, we need to include its cost.
            if (initialBill == null || initialBill.Status != 2)
            {
                decimal courtFee = session.CourtFeePerPerson ?? 0;
                if (courtFee > 0)
                {
                    dto.LineItems.Add(new BillLineItemDto { Description = "ค่าสนาม", Amount = courtFee });
                }

                decimal serviceFee = _configuration.GetValue<decimal>("ServiceFee");
                if (serviceFee > 0)
                {
                    dto.LineItems.Add(new BillLineItemDto { Description = "ค่าธรรมเนียม", Amount = serviceFee });
                }
            }

            // 2. Shuttlecock Fee
            var playedMatchesCount = await _context.Matches
                .CountAsync(m => m.SessionId == sessionId && m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == userId));

            decimal shuttleTotal = 0;
            if (session.CostingMethod == 2 && session.ShuttlecockFeePerPerson.HasValue) // Buffet
            {
                shuttleTotal = session.ShuttlecockFeePerPerson.Value;
                if (shuttleTotal > 0)
                {
                    dto.LineItems.Add(new BillLineItemDto { Description = "ค่าลูกแบด (เหมาจ่าย)", Amount = shuttleTotal });
                }
            }
            else if (session.ShuttlecockFeePerPerson.HasValue) // Per game
            {
                shuttleTotal = (session.ShuttlecockFeePerPerson.Value) * playedMatchesCount;
                if (shuttleTotal > 0)
                {
                    dto.LineItems.Add(new BillLineItemDto { Description = $"ค่าลูกแบด ({playedMatchesCount} เกม)", Amount = shuttleTotal });
                }
            }

            return dto;
        }

        public async Task<PlayerStatsDto?> GetMyStatsAsync(int sessionId, int userId)
        {
            var playedMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == userId))
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin)
                .OrderBy(m => m.StartTime)
                .ToListAsync();

            var stats = new PlayerStatsDto();
            stats.TotalGamesPlayed = playedMatches.Count;
            int totalMinutes = 0;

            foreach(var match in playedMatches)
            {
                var myMatchPlayer = match.MatchPlayers.First(mp => mp.UserId == userId);
                int duration = (match.StartTime.HasValue && match.EndTime.HasValue) ? (int)(match.EndTime.Value - match.StartTime.Value).TotalMinutes : 0;
                totalMinutes += duration;

                if (myMatchPlayer.Result == 1) stats.Wins++;
                else if (myMatchPlayer.Result == 2) stats.Losses++;

                stats.MatchHistory.Add(new PlayerMatchHistoryItemDto
                {
                    MatchId = match.MatchId,
                    CourtNumber = match.CourtNumber,
                    DurationMinutes = duration,
                    Result = myMatchPlayer.Result,
                    Notes = myMatchPlayer.Notes,
                    Teammate = match.MatchPlayers.Where(mp => mp.Team == myMatchPlayer.Team && mp.UserId != userId)
                        .Select(mp => new PlayerInMatchDto { 
                            UserId = mp.UserId,
                            WalkinId = mp.WalkinId,
                            Nickname = mp.UserId.HasValue ? mp.User.UserProfile.Nickname : mp.Walkin?.GuestName ?? "N/A",
                            ProfilePhotoUrl = mp.UserId.HasValue ? mp.User.UserProfile.ProfilePhotoUrl : null 
                        }).FirstOrDefault() ?? new PlayerInMatchDto { Nickname = "N/A" },
                    Opponents = match.MatchPlayers.Where(mp => mp.Team != myMatchPlayer.Team)
                        .Select(mp => new PlayerInMatchDto { 
                            UserId = mp.UserId,
                            WalkinId = mp.WalkinId,
                            Nickname = mp.UserId.HasValue ? mp.User.UserProfile.Nickname : mp.Walkin?.GuestName ?? "N/A",
                            ProfilePhotoUrl = mp.UserId.HasValue ? mp.User.UserProfile.ProfilePhotoUrl : null 
                        }).ToList()
                });
            }
            stats.TotalMinutesPlayed = totalMinutes.ToString();
            return stats;
        }

        public async Task<(bool Success, string ErrorMessage)> SubmitMatchResultAsync(int matchId, int userId, SubmitMatchResultDto dto)
        {
            var matchPlayer = await _context.MatchPlayers.FirstOrDefaultAsync(mp => mp.MatchId == matchId && mp.UserId == userId);
            if (matchPlayer == null) return (false, "Match not found or you are not in this match.");

            // บันทึกผล, Note, และประวัติการอัปเดต
            matchPlayer.Result = (byte?)dto.Result; // FIX: แก้จาก short? เป็น byte?
            matchPlayer.Notes = dto.Notes;
            matchPlayer.UpdatedBy = userId;
            matchPlayer.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, "Result submitted successfully.");
        }

        public async Task<(bool Success, string ErrorMessage)> CheckoutAndPayAsync(int sessionId, int userId, PlayerPaymentRequestDto dto)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null)
            {
                return (false, "Session not found.");
            }

            var participant = await _context.SessionParticipants
                .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);

            if (participant == null)
            {
                return (false, "You are not part of this session.");
            }

            // --- NEW: ตรวจสอบว่ากำลังเล่นอยู่หรือไม่ ถ้าเล่นอยู่ห้าม Checkout ---
            var isPlaying = await _context.MatchPlayers
                .AnyAsync(mp => mp.Match.SessionId == sessionId && mp.Match.Status == 1 && mp.UserId == userId);

            if (isPlaying)
            {
                return (false, "คุณกำลังแข่งขันอยู่ในสนาม ไม่สามารถชำระเงินเพื่อเช็คเอาท์ได้ในขณะนี้");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var finalLineItems = new List<BillLineItem>();
                    decimal finalTotalAmount = 0;

                    // ดึงประวัติบิลที่ชำระเงินแล้วเพื่อหักลบกลบยอด (ให้เหมือนฝั่ง Organizer)
                    var pastBills = await _context.ParticipantBills.Include(b => b.BillLineItems)
                        .Where(b => b.SessionId == sessionId && b.UserId == userId && b.Status == 2)
                        .ToListAsync();

                    bool courtFeePaid = pastBills.Any(b => b.BillLineItems.Any(li => li.Description == "ค่าสนาม"));
                    bool servicePaid = pastBills.Any(b => b.BillLineItems.Any(li => li.Description == "ค่าธรรมเนียม"));

                    // 1. Court Fee & Service Fee
                    if (!courtFeePaid)
                    {
                        decimal courtFee = session.CourtFeePerPerson ?? 0;
                        if (courtFee > 0)
                        {
                            finalLineItems.Add(new BillLineItem { Description = "ค่าสนาม", Amount = courtFee });
                            finalTotalAmount += courtFee;
                        }
                    }
                    if (!servicePaid)
                    {
                        decimal serviceFee = _configuration.GetValue<decimal>("ServiceFee");
                        if (serviceFee > 0)
                        {
                            finalLineItems.Add(new BillLineItem { Description = "ค่าธรรมเนียม", Amount = serviceFee });
                            finalTotalAmount += serviceFee;
                        }
                    }

                    // 2. Shuttlecock Fee
                    var matchesPlayed = await _context.Matches
                        .Where(m => m.SessionId == session.SessionId && m.Status == 2 &&
                                    m.MatchPlayers.Any(mp => mp.UserId == userId))
                        .CountAsync();

                    decimal shuttleTotal = 0;
                    if (session.CostingMethod == 2 && session.ShuttlecockFeePerPerson.HasValue) // Buffet
                    {
                        shuttleTotal = session.ShuttlecockFeePerPerson.Value;
                    }
                    else if (session.ShuttlecockFeePerPerson.HasValue) // Per game
                    {
                        shuttleTotal = (session.ShuttlecockFeePerPerson.Value) * matchesPlayed;
                    }

                    decimal paidShuttle = pastBills
                        .SelectMany(b => b.BillLineItems)
                        .Where(li => li.Description.StartsWith("ค่าลูกแบด"))
                        .Sum(li => li.Amount);

                    decimal dueShuttle = shuttleTotal - paidShuttle;
                    if (dueShuttle > 0)
                    {
                        finalLineItems.Add(new BillLineItem { Description = session.CostingMethod == 2 ? "ค่าลูกแบด (เหมาจ่าย)" : $"ค่าลูกแบด ({matchesPlayed} เกม)", Amount = dueShuttle });
                        finalTotalAmount += dueShuttle;
                    }

                    // 3. Custom Items
                    if (dto.CustomItems != null && dto.CustomItems.Any())
                    {
                        foreach (var item in dto.CustomItems)
                        {
                            finalLineItems.Add(new BillLineItem { Description = item.Description, Amount = item.Amount });
                            finalTotalAmount += item.Amount;
                        }
                    }
                    else
                    {
                        // ดึงจากบิลค้างชำระที่ผู้จัดเพิ่มเข้ามาก่อนหน้า (ถ้ามี)
                        var pendingBills = await _context.ParticipantBills.Include(b => b.BillLineItems)
                            .Where(b => b.SessionId == sessionId && b.UserId == userId && b.Status == 1)
                            .ToListAsync();

                        if (pendingBills.Any())
                        {
                            var latestPending = pendingBills.OrderByDescending(b => b.CreatedDate).First();
                            var customItems = latestPending.BillLineItems.Where(li =>
                                li.Description != "ค่าสนาม" &&
                                li.Description != "ค่าธรรมเนียม" && !li.Description.StartsWith("ค่าลูกแบด"));
                            
                            foreach (var item in customItems)
                            {
                                finalLineItems.Add(new BillLineItem { Description = item.Description, Amount = item.Amount });
                                finalTotalAmount += item.Amount;
                            }

                            // ยกเลิกบิลที่ค้างชำระทั้งหมด เพื่อสร้างบิลใหม่ใบเดียวตามรูปแบบผู้จัด
                            foreach (var pb in pendingBills)
                            {
                                pb.Status = 3; // 3 = Cancelled
                            }
                        }
                    }

                    if (finalTotalAmount < 0) finalTotalAmount = 0;

                    var newBill = new ParticipantBill
                    {
                        SessionId = sessionId,
                        UserId = userId,
                        TotalAmount = finalTotalAmount,
                        Status = 2, // Paid
                        CreatedDate = DateTime.UtcNow,
                        BillLineItems = finalLineItems
                    };
                    
                    _context.ParticipantBills.Add(newBill);

                    // บันทึกเวลา Checkout เฉพาะกรณีที่ยังไม่เคย Checkout มาก่อน
                    if (participant.CheckoutTime == null)
                    {
                        participant.CheckoutTime = DateTime.UtcNow;
                    }

                    if (finalTotalAmount >= 0)
                    {
                        var payment = new Payment
                        {
                            PaymentMethod = dto.PaymentMethod == "QR Code" ? (byte)2 : (byte)1,
                            Amount = finalTotalAmount,
                            PaymentDate = DateTime.UtcNow,
                        };
                        newBill.Payments.Add(payment);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var liveState = await _matchManagementService.GetLiveStateAsync(sessionId, session.CreatedByUserId);
                    await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("ReceiveLiveStateUpdate", liveState);

                    // --- แจ้งเตือนผู้จัด (ถ้ามียอดชำระ) ---
                    if (finalTotalAmount > 0)
                    {
                        var user = await _context.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.UserId == userId);
                        await _notificationService.SendNotificationAsync(session.CreatedByUserId, "ได้รับชำระเงิน", $"{user?.UserProfile?.Nickname ?? "ผู้เล่น"} ได้ชำระเงินจำนวน {finalTotalAmount:N2} บาท", "PAYMENT_RECEIVED", sessionId);
                    }

                    return (true, "Payment successful and checked out.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return (false, $"An error occurred: {ex.Message}");
                }
            });
        }

        public async Task<(bool Success, string ErrorMessage)> TogglePauseAsync(int sessionId, int userId, bool isPaused)
        {
            var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);
            if (participant == null) return (false, "Participant not found.");

            // สร้าง ID ให้ตรงกับฟอร์แมตใน Flutter App ของผู้จัด
            string playerId = $"Member_{participant.ParticipantId}";

            // ส่ง SignalR แจ้งเตือนไปยังแอปผู้จัด (Group session-{sessionId})
            await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("PlayerPauseStateChanged", new { PlayerId = playerId, IsPaused = isPaused });
            
            return (true, "Pause state updated.");
        }

        public async Task<(bool Success, string ErrorMessage)> ToggleBookmarkAsync(int sessionId, int userId, bool isBookmark)
        {
            var existing = await _context.UserBookmarkedSessions.FirstOrDefaultAsync(b => b.UserId == userId && b.SessionId == sessionId);
            if (isBookmark)
            {
                if (existing == null)
                {
                    await _context.UserBookmarkedSessions.AddAsync(new UserBookmarkedSession { UserId = userId, SessionId = sessionId, CreatedDate = DateTime.UtcNow });
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                if (existing != null)
                {
                    _context.UserBookmarkedSessions.Remove(existing);
                    await _context.SaveChangesAsync();
                }
            }
            return (true, string.Empty);
        }

        public async Task<OrganizerSummaryDto?> GetOrganizerSummaryAsync(int organizerId, int? currentUserId)
        {
            var user = await _context.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.UserId == organizerId);
            if (user == null) return null;

            bool isFollowed = false;
            if (currentUserId.HasValue)
            {
                isFollowed = await _context.UserFollows
                    .AnyAsync(f => f.FollowerId == currentUserId.Value && f.OrganizerId == organizerId);
            }

            var sessions = await _context.GameSessions.Where(s => s.CreatedByUserId == organizerId).ToListAsync();
            
            return new OrganizerSummaryDto
            {
                OrganizerId = organizerId,
                Nickname = user.UserProfile?.Nickname ?? "N/A",
                ProfilePhotoUrl = user.UserProfile?.ProfilePhotoUrl,
                TotalHosted = sessions.Count(s => s.Status != 3), // นับก๊วนที่ไม่ได้ถูกยกเลิกว่าเป็นการจัด
                TotalCancelled = sessions.Count(s => s.Status == 3), // ยกเลิก (Status = 3)
                IsFollowed = isFollowed
            };
        }
    }
}
