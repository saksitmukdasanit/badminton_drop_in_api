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

        public PlayerGameSessionService(
            BadmintonDbContext context, 
            IConfiguration configuration,
            IHubContext<ManagementGameHub> hubContext,
            IMatchManagementService matchManagementService)
        {
            _context = context;
            _configuration = configuration;
            _hubContext = hubContext;
            _matchManagementService = matchManagementService;
        }

        public async Task<IEnumerable<UpcomingSessionCardDto>> GetUpcomingSessionsAsync(int? currentUserId, string? keyword = null, string? sortBy = null, int page = 1, int limit = 10)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var thaiCulture = new CultureInfo("th-TH");
            var userBookmarks = new HashSet<int>();

            var query = _context.GameSessions
                .Where(s => s.SessionDate >= today && s.Status == 1);

            // กรองก๊วนที่ผู้เล่นเข้าร่วมแล้ว (ตัวจริง หรือ ตัวสำรอง) ออกจากการค้นหาเพื่อไม่ให้สับสน
            if (currentUserId.HasValue)
            {
                query = query.Where(s => !s.SessionParticipants.Any(p => p.UserId == currentUserId.Value && (p.Status == 1 || p.Status == 2)));
            }

            // 1. กรองข้อมูล (Search) จากชื่อก๊วน หรือ ชื่อสนาม ด้วย DB โดยตรง
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lowerKeyword = keyword.ToLower();
                query = query.Where(s => s.GroupName.ToLower().Contains(lowerKeyword) ||
                                         (s.Venue != null && s.Venue.VenueName.ToLower().Contains(lowerKeyword)));
            }

            // 2. Map ข้อมูลให้เป็น DTO เพื่อประหยัด Memory ก้อนใหญ่
            var projectedQuery = query.Select(s => new UpcomingSessionCardDto
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
                    Price = (s.CourtFeePerPerson.HasValue || s.ShuttlecockFeePerPerson.HasValue)
                          ? $"{(s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0):N0} บาท" : "สอบถามผู้จัด",
                    OrganizerName = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.Nickname : "N/A",
                    OrganizerImageUrl = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.ProfilePhotoUrl : null,
                    IsBookmarked = userBookmarks.Contains(s.SessionId),
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1) + s.SessionWalkinGuests.Count(g => g.Status == 1),
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
                    UserStatus = currentUserId.HasValue
                        ? s.SessionParticipants
                            .Where(p => p.UserId == currentUserId.Value)
                            .Select(p => p.Status == 1 ? (p.CheckinTime != null ? "CheckedIn" : "Joined") : p.Status == 2 ? "Waitlisted" : p.Status == 3 ? "Refund" : "NotJoined")
                            .FirstOrDefault() ?? "NotJoined"
                        : "NotJoined"
                });

            var result = await projectedQuery.ToListAsync();

            // 3. จัดเรียงข้อมูล (Sort) 
            if (sortBy == "ค่าสนาม")
            {
                result = result.OrderBy(d => 
                    (decimal.TryParse(d.CourtFeePerPerson, out var c) ? c : 0) + 
                    (decimal.TryParse(d.ShuttlecockFeePerPerson, out var sh) ? sh : 0)
                ).ToList();
            }
            else
            {
                // ค่าเริ่มต้น (เรียงตามวันและเวลาที่เร็วที่สุดขึ้นก่อน)
                result = result.OrderBy(d => d.SessionStart).ToList();
            }

            // 4. แบ่งหน้า (Pagination)
            result = result.Skip((page - 1) * limit).Take(limit).ToList();

            return result;
        }

        public async Task<MyGameSessionsResponseDto> GetMySessionsAsync(int userId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var thaiCulture = new CultureInfo("th-TH");
            var userBookmarks = new HashSet<int>();

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
                .Include(s => s.GameType)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand)
                .OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.StartTime)
                .ToListAsync();

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
                    Price = (s.CourtFeePerPerson.HasValue || s.ShuttlecockFeePerPerson.HasValue)
                          ? $"{(s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0):N0} บาท" : "สอบถามผู้จัด",
                    OrganizerName = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.Nickname : "N/A",
                    OrganizerImageUrl = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.ProfilePhotoUrl : null,
                    IsBookmarked = userBookmarks.Contains(s.SessionId),
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1) + s.SessionWalkinGuests.Count(g => g.Status == 1),
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
                    UserStatus = userStatusStr // ส่งสถานะของ User กลับไปที่ Flutter
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
            var userBookmarks = new HashSet<int>();

            // 1. ดึงเฉพาะก๊วนที่ผู้เล่นคนนี้มีชื่ออยู่ และ "วันที่ผ่านไปแล้ว"
            var query = _context.GameSessions
                .Where(s => s.SessionParticipants.Any(p => p.UserId == userId))
                // ดึงมาแสดงถ้า 1) วันที่ผ่านไปแล้ว 2) ก๊วนนั้นจบแล้ว(Status=4) หรือ 3) ตัวผู้เล่นเอง Checkout/จ่ายเงินไปแล้ว
                .Where(s => s.SessionDate < today || s.Status == 4 || s.SessionParticipants.Any(p => p.UserId == userId && p.CheckoutTime != null));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lowerKeyword = keyword.ToLower();
                query = query.Where(s => s.GroupName.ToLower().Contains(lowerKeyword) ||
                                         (s.Venue != null && s.Venue.VenueName.ToLower().Contains(lowerKeyword)));
            }

            var projectedQuery = query.Select(s => new UpcomingSessionCardDto
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
                    Price = (s.CourtFeePerPerson.HasValue || s.ShuttlecockFeePerPerson.HasValue)
                          ? $"{(s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0):N0} บาท" : "สอบถามผู้จัด",
                    OrganizerName = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.Nickname : "N/A",
                    OrganizerImageUrl = s.CreatedByUser != null && s.CreatedByUser.UserProfile != null ? s.CreatedByUser.UserProfile.ProfilePhotoUrl : null,
                    IsBookmarked = userBookmarks.Contains(s.SessionId),
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1) + s.SessionWalkinGuests.Count(g => g.Status == 1),
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
                    UserStatus = s.SessionParticipants.Where(p => p.UserId == userId).Select(p => p.Status == 1 ? (p.CheckoutTime != null ? "CheckedOut" : (p.CheckinTime != null ? "CheckedIn" : "Joined")) : p.Status == 2 ? "Waitlisted" : p.Status == 3 ? "Refund" : "NotJoined").FirstOrDefault() ?? "NotJoined"
                });

            var result = await projectedQuery.ToListAsync();

            // สำหรับประวัติ ให้เรียงจาก "วันที่ล่าสุดไปเก่าสุด" เป็นค่าเริ่มต้น
            result = result.OrderByDescending(d => d.SessionStart).ToList();
            result = result.Skip((page - 1) * limit).Take(limit).ToList();
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
            var bill = session.ParticipantBills.OrderByDescending(b => b.CreatedDate).FirstOrDefault(b => b.UserId == userId && b.Status != 3);
            if (bill != null)
            {
                result.Payment.Status = bill.Status == 2 ? "Completed" : "Pending";
                result.Payment.TotalAmount = bill.TotalAmount;
                result.Payment.CourtFee = bill.BillLineItems.Where(li => li.Description.Contains("คอร์ท")).Sum(li => li.Amount);
                result.Payment.ServiceFee = bill.BillLineItems.Where(li => li.Description.Contains("ธรรมเนียม")).Sum(li => li.Amount);

                if (bill.Status == 2)
                {
                    var payment = await _context.Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefaultAsync(p => p.BillId == bill.BillId);
                    if (payment != null)
                    {
                        result.Payment.PaymentDate = payment.PaymentDate.AddHours(7).ToString("dd/MM/yy HH:mm น.");
                        result.Payment.PaymentMethod = payment.PaymentMethod == 1 ? "Cash" : "QR Code";
                    }
                }
            }
            else
            {
                result.Payment.Status = "Pending";
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
                    MyTeam = match.MatchPlayers.Where(mp => mp.Team == myMatchPlayer.Team).Select(mp => new PlayerInMatchDto { Nickname = mp.UserId.HasValue ? mp.User.UserProfile.Nickname : mp.Walkin?.GuestName ?? "N/A", ProfilePhotoUrl = mp.UserId.HasValue ? mp.User.UserProfile.ProfilePhotoUrl : null }).ToList(),
                    Opponents = match.MatchPlayers.Where(mp => mp.Team != myMatchPlayer.Team).Select(mp => new PlayerInMatchDto { Nickname = mp.UserId.HasValue ? mp.User.UserProfile.Nickname : mp.Walkin?.GuestName ?? "N/A", ProfilePhotoUrl = mp.UserId.HasValue ? mp.User.UserProfile.ProfilePhotoUrl : null }).ToList()
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

        public async Task<(JoinSessionResponseDto? Data, string ErrorMessage)> JoinSessionAsync(int sessionId, int userId)
        {
            var session = await _context.GameSessions
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return (null, "Session not found.");
            if (session.Status != 1) return (null, "This session is no longer open for booking.");
            if (session.CreatedByUserId == userId) return (null, "Organizers cannot join their own session as a participant.");

            var existingParticipant = session.SessionParticipants.FirstOrDefault(p => p.UserId == userId);
            if (existingParticipant != null && existingParticipant.Status != 3) return (null, "You are already registered for this session.");

            int organizerUserId = session.CreatedByUserId;
            int? savedSkillLevelId = await _context.UserOrganizerSkills.Where(uos => uos.OrganizerUserId == organizerUserId && uos.UserId == userId).Select(uos => (int?)uos.SkillLevelId).FirstOrDefaultAsync();

            var activeParticipants = session.SessionParticipants.Count(p => p.Status == 1) + session.SessionWalkinGuests.Count(g => g.Status == 1);
            var waitlistedParticipants = session.SessionParticipants.Count(p => p.Status == 2) + session.SessionWalkinGuests.Count(g => g.Status == 2);

            int newStatus = (activeParticipants < session.MaxParticipants && waitlistedParticipants == 0) ? 1 : 2;
            string statusMessage = newStatus == 1 ? "Joined successfully." : "You are on the waitlist.";

            SessionParticipant newParticipantEntry = existingParticipant ?? new SessionParticipant { SessionId = sessionId, UserId = userId };
            newParticipantEntry.Status = (byte)newStatus;
            newParticipantEntry.JoinedDate = DateTime.UtcNow;
            newParticipantEntry.SkillLevelId = savedSkillLevelId;

            if (existingParticipant == null) await _context.SessionParticipants.AddAsync(newParticipantEntry);
            await _context.SaveChangesAsync();
            return (new JoinSessionResponseDto { ParticipantId = newParticipantEntry.ParticipantId, Status = newStatus, StatusMessage = statusMessage }, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> CancelBookingAsync(int sessionId, int userId)
        {
            var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);
            if (participant == null || participant.Status == 3) return (false, "Booking not found.");
            participant.Status = 3;
            await _context.SaveChangesAsync();
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
                    dto.LineItems.Add(new BillLineItemDto { Description = "ค่าคอร์ท", Amount = courtFee });
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
                        .Select(mp => new PlayerInMatchDto { Nickname = mp.UserId.HasValue ? mp.User.UserProfile.Nickname : mp.Walkin?.GuestName ?? "N/A" }).FirstOrDefault() ?? new PlayerInMatchDto { Nickname = "N/A" },
                    Opponents = match.MatchPlayers.Where(mp => mp.Team != myMatchPlayer.Team)
                        .Select(mp => new PlayerInMatchDto { Nickname = mp.UserId.HasValue ? mp.User.UserProfile.Nickname : mp.Walkin?.GuestName ?? "N/A" }).ToList()
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

            if (participant.CheckoutTime != null)
            {
                return (false, "You have already checked out.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var finalLineItems = new List<BillLineItem>();
                    decimal finalTotalAmount = 0;

                    // 1. Court Fee & Service Fee
                    var initialBill = await _context.ParticipantBills
                        .AsNoTracking()
                        .Include(b => b.BillLineItems)
                        .FirstOrDefaultAsync(b => b.SessionId == sessionId && b.UserId == userId && b.Status != 3);

                    bool courtFeePaid = initialBill?.Status == 2 && initialBill.BillLineItems.Any(li => li.Description == "ค่าคอร์ท");

                    if (!courtFeePaid)
                    {
                        decimal courtFee = session.CourtFeePerPerson ?? 0;
                        if (courtFee > 0)
                        {
                            finalLineItems.Add(new BillLineItem { Description = "ค่าคอร์ท", Amount = courtFee });
                            finalTotalAmount += courtFee;
                        }
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
                        if (shuttleTotal > 0) finalLineItems.Add(new BillLineItem { Description = "ค่าลูกแบด (เหมาจ่าย)", Amount = shuttleTotal });
                    }
                    else if (session.ShuttlecockFeePerPerson.HasValue) // Per game
                    {
                        shuttleTotal = (session.ShuttlecockFeePerPerson.Value) * matchesPlayed;
                        if (shuttleTotal > 0) finalLineItems.Add(new BillLineItem { Description = $"ค่าลูกแบด ({matchesPlayed} เกม)", Amount = shuttleTotal });
                    }
                    finalTotalAmount += shuttleTotal;

                    // 3. Custom Items
                    if (dto.CustomItems != null && dto.CustomItems.Any())
                    {
                        foreach (var item in dto.CustomItems)
                        {
                            finalLineItems.Add(new BillLineItem { Description = item.Description, Amount = item.Amount });
                            finalTotalAmount += item.Amount;
                        }
                    }
                    if (finalTotalAmount < 0) finalTotalAmount = 0;

                    var billToUpdate = await _context.ParticipantBills
                        .Include(b => b.BillLineItems)
                        .FirstOrDefaultAsync(b => b.SessionId == sessionId && b.UserId == userId && b.Status != 3);

                    if (billToUpdate == null)
                    {
                        billToUpdate = new ParticipantBill { SessionId = sessionId, UserId = userId, CreatedDate = DateTime.UtcNow };
                        _context.ParticipantBills.Add(billToUpdate);
                        billToUpdate.BillLineItems = finalLineItems;
                    }
                    else
                    {
                        _context.BillLineItems.RemoveRange(billToUpdate.BillLineItems);
                        billToUpdate.BillLineItems = finalLineItems;
                    }

                    billToUpdate.TotalAmount = finalTotalAmount;
                    billToUpdate.Status = 2; // Paid

                    participant.CheckoutTime = DateTime.UtcNow;

                    var payment = new Payment
                    {
                        PaymentMethod = dto.PaymentMethod == "QR Code" ? (byte)2 : (byte)1,
                        Amount = finalTotalAmount,
                        PaymentDate = DateTime.UtcNow,
                    };
                    // ผูก Object Payment เข้ากับ Bill โดยตรง เพื่อให้ EF Core จัดการ Foreign Key (BillId) ให้อัตโนมัติเมื่อกด Save
                    billToUpdate.Payments.Add(payment);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var liveState = await _matchManagementService.GetLiveStateAsync(sessionId, session.CreatedByUserId);
                    await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("ReceiveLiveStateUpdate", liveState);

                    return (true, "Payment successful and checked out.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return (false, $"An error occurred: {ex.Message}");
                }
            });
        }
    }
}
