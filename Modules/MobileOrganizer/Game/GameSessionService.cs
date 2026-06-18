using System.Globalization;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Hubs;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DropInBadAPI.Constants;

namespace DropInBadAPI.Service.Mobile.Game
{
    public class GameSessionService : IGameSessionService
    {
        private readonly BadmintonDbContext _context;
        private readonly IHubContext<ManagementGameHub> _hubContext;
        private readonly IMatchManagementService _matchManagementService;
        private readonly IConfiguration _configuration;
        private readonly INotificationService _notificationService;
        private readonly IGameSessionBillingService _billingService;
        private readonly IGameSessionBookingService _bookingService;
        private readonly IAutoMatchService _autoMatchService;

        public GameSessionService(
            BadmintonDbContext context,
            IHubContext<ManagementGameHub> hubContext,
            IMatchManagementService matchManagementService,
            IConfiguration configuration,
            INotificationService notificationService,
            IGameSessionBillingService billingService,
            IGameSessionBookingService bookingService,
            IAutoMatchService autoMatchService)
        {
            _context = context;
            _hubContext = hubContext;
            _matchManagementService = matchManagementService;
            _configuration = configuration;
            _notificationService = notificationService;
            _billingService = billingService;
            _bookingService = bookingService;
            _autoMatchService = autoMatchService;
        }

        public async Task<ManageGameSessionDto> CreateSessionAsync(int organizerUserId, SaveGameSessionDto dto, bool notifyFollowers = true)
        {
            if (dto.StartTime >= dto.EndTime)
            {
                throw new Exception("เวลาเริ่มต้น ต้องน้อยกว่า เวลาสิ้นสุด");
            }

            // --- 1. เช็คเวลาและสนามทับซ้อนก่อนสร้าง ---
            var overlappingSession = await _context.GameSessions
                .Include(s => s.Venue)
                .Where(s => s.CreatedByUserId == organizerUserId
                         && s.Status != 3 && s.Status != 4 // ไม่เช็คก๊วนที่ยกเลิกหรือจบไปแล้ว
                         && s.SessionDate == dto.SessionDate
                         && s.Venue.GooglePlaceId == dto.VenueData.GooglePlaceId
                         && s.StartTime < dto.EndTime
                         && s.EndTime > dto.StartTime)
                .FirstOrDefaultAsync();

            if (overlappingSession != null)
            {
                string timeOld = $"{overlappingSession.StartTime:hh\\:mm} - {overlappingSession.EndTime:hh\\:mm}";
                throw new Exception($"พบก๊วนซ้ำ!\nคุณได้สร้างก๊วน \"{overlappingSession.GroupName}\"\nที่สนามนี้ในเวลา {timeOld} ไว้แล้ว\nกรุณาตรวจสอบเวลาอีกครั้ง");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    int venueId;
                    var existingVenue = await _context.Venues
                        .FirstOrDefaultAsync(v => v.GooglePlaceId == dto.VenueData.GooglePlaceId);

                    if (existingVenue != null)
                    {
                        venueId = existingVenue.VenueId;
                    }
                    else
                    {
                        var newVenue = new Venue
                        {
                            GooglePlaceId = dto.VenueData.GooglePlaceId,
                            VenueName = dto.VenueData.Name,
                            Address = dto.VenueData.Address,
                            Latitude = dto.VenueData.Latitude,
                            Longitude = dto.VenueData.Longitude,
                            FirstUsedByUserId = organizerUserId
                        };
                        await _context.Venues.AddAsync(newVenue);
                        await _context.SaveChangesAsync();
                        venueId = newVenue.VenueId;
                    }
                    // --------------------------------------------------

                    var newSession = new GameSession
                    {
                        CreatedByUserId = organizerUserId,
                        GroupName = dto.GroupName,
                        VenueId = venueId,
                        SessionDate = dto.SessionDate,
                        StartTime = dto.StartTime,
                        EndTime = dto.EndTime,
                        MaxParticipants = dto.MaxParticipants,
                        GameTypeId = dto.GameTypeId,
                        PairingMethodId = dto.PairingMethodId,
                        CostingMethod = (byte?)dto.CostingMethod,
                        CourtFeePerPerson = dto.CourtFeePerPerson,
                        ShuttlecockFeePerPerson = dto.ShuttlecockFeePerPerson,
                        TotalCourtCost = dto.TotalCourtCost,
                        ShuttlecockCostPerUnit = dto.ShuttlecockCostPerUnit,
                        ShuttlecockModelId = dto.ShuttlecockModelId,
                        NumberOfCourts = dto.NumberOfCourts,
                        CourtNumbers = dto.CourtNumbers,
                        Notes = dto.Notes,
                        Status = 1 // 1=เปิดรับ
                    };
                    await _context.GameSessions.AddAsync(newSession);
                    await _context.SaveChangesAsync();

                    if (dto.FacilityIds != null && dto.FacilityIds.Any())
                    {
                        var facilities = dto.FacilityIds.Select(id => new GameSessionFacility { SessionId = newSession.SessionId, FacilityId = id, CreatedBy = organizerUserId });
                        await _context.GameSessionFacilities.AddRangeAsync(facilities);
                    }

                    if (dto.PhotoUrls != null && dto.PhotoUrls.Any())
                    {
                        var photos = dto.PhotoUrls.Select((url, i) => new GameSessionPhoto { SessionId = newSession.SessionId, PhotoUrl = url, DisplayOrder = (byte)(i + 1), CreatedBy = organizerUserId });
                        await _context.GameSessionPhotos.AddRangeAsync(photos);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    if (notifyFollowers)
                    {
                        // --- แจ้งเตือนผู้ติดตาม (Followers) ของผู้จัดเกี่ยวกับก๊วนใหม่ ---
                        var organizer = await _context.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.UserId == organizerUserId);
                        var followers = await _context.UserFollows.Where(f => f.OrganizerId == organizerUserId).Select(f => f.FollowerId).ToListAsync();

                        var thaiCulture = new CultureInfo("th-TH");
                        string sessionDateStr = newSession.SessionDate.ToString("dd/MM/yyyy", thaiCulture);
                        string timeStr = $"{newSession.StartTime.ToString("HH:mm")} - {newSession.EndTime.ToString("HH:mm")} น.";
                        string notiMessage = $"'{organizer?.UserProfile?.Nickname}' ได้สร้างก๊วน '{newSession.GroupName}' วันที่ {sessionDateStr} เวลา {timeStr}";

                        foreach (var followerId in followers)
                        {
                            await _notificationService.SendNotificationAsync(
                                followerId,
                                "ก๊วนใหม่จากผู้จัดที่คุณติดตาม",
                                notiMessage,
                                "NEW_SESSION_FROM_FOLLOWED_ORGANIZER",
                                newSession.SessionId);
                        }
                    }

                    // คืนค่าหลังจาก Commit สำเร็จ
                    return (await GetSessionForManageViewAsync(newSession.SessionId, organizerUserId))!;
                }
                catch (Exception)
                {
                    // ไม่ต้อง Rollback ตรงนี้แล้ว เพราะถ้าเกิด Exception ก่อน Commit, Transaction จะ Rollback เอง
                    // await transaction.RollbackAsync(); // << เอาออกได้
                    throw; // ปล่อยให้ strategy จัดการ Error หรือลองใหม่
                }
            }); // <-- ปิด ExecuteAsync
        }

        public async Task<ManageGameSessionDto?> GetSessionForManageViewAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions
                .Where(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId)
                .Include(s => s.Venue)
                .Include(s => s.ShuttlecockModel)
                    .ThenInclude(m => m!.Brand)
                .Include(s => s.GameType) // เพิ่ม Include GameType
                .Include(s => s.GameSessionPhotos)
                .Select(s => new ManageGameSessionDto
                {
                    SessionId = s.SessionId,
                    SessionPublicId = s.SessionPublicId,
                    GroupName = s.GroupName,
                    Status = s.Status ?? 1,
                    SessionStart = s.SessionDate.ToDateTime(s.StartTime),
                    SessionEnd = s.SessionDate.ToDateTime(s.EndTime),
                    VenueName = s.Venue.VenueName,
                    VenueAddress = s.Venue.Address,
                    ShuttlecockBrandName = s.ShuttlecockModel != null ? s.ShuttlecockModel.Brand.BrandName : null,
                    ShuttlecockModelName = s.ShuttlecockModel != null ? s.ShuttlecockModel.ModelName : null,
                    ShuttlecockCostPerUnit = s.ShuttlecockCostPerUnit,
                    CourtFeePerPerson = s.CourtFeePerPerson,
                    MaxParticipants = s.MaxParticipants,
                    GameTypeName = s.GameType != null ? s.GameType.TypeName : null, // Map ข้อมูล
                    Notes = s.Notes,
                    PhotoUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList()
                })
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return null;
            }

            // --- NEW: คำนวณจำนวนเกมที่เล่นจบแล้วของทุกคน ---
            var finishedMatchPlayers = await _context.MatchPlayers
                .Where(mp => mp.Match.SessionId == sessionId && mp.Match.Status == 2)
                .Select(mp => new { mp.UserId, mp.WalkinId })
                .ToListAsync();

            var memberGameCounts = finishedMatchPlayers
                .Where(mp => mp.UserId.HasValue)
                .GroupBy(mp => mp.UserId)
                .ToDictionary(g => g.Key!.Value, g => g.Count());

            var guestGameCounts = finishedMatchPlayers
                .Where(mp => mp.WalkinId.HasValue)
                .GroupBy(mp => mp.WalkinId)
                .ToDictionary(g => g.Key!.Value, g => g.Count());
            // ---------------------------------------------------

            var registeredParticipants = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId)
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .ToListAsync(); // ดึงข้อมูลมาก่อน

            var walkinGuests = await _context.SessionWalkinGuests
                .Where(g => g.SessionId == sessionId)
                .Include(g => g.SkillLevel)
                .ToListAsync(); // ดึงข้อมูลมาก่อน

            // Map ข้อมูลพร้อมใส่จำนวนเกม
            session.Participants.AddRange(registeredParticipants.Select(p => ParticipantDtoMapper.FromMember(p, memberGameCounts.ContainsKey(p.UserId) ? memberGameCounts[p.UserId] : 0)));
            session.Participants.AddRange(walkinGuests.Select(g => ParticipantDtoMapper.FromGuest(g, guestGameCounts.ContainsKey(g.WalkinId) ? guestGameCounts[g.WalkinId] : 0)));

            session.Participants = session.Participants.OrderBy(p => p.Status).ThenBy(p => p.ParticipantId).ToList();
            session.CurrentParticipants = session.Participants.Count(p => p.Status == 1);

            return session;
        }

        public async Task<EditGameSessionDto?> GetSessionForEditAsync(int sessionId)
        {
            var session = await _context.GameSessions
                .Where(s => s.SessionId == sessionId)
                .Include(s => s.Venue)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand) // เพิ่ม Include Brand
                .Include(s => s.GameType) // เพิ่ม Include GameType
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.GameSessionFacilities)
                .Select(s => new EditGameSessionDto
                {
                    SessionId = s.SessionId,
                    SessionPublicId = s.SessionPublicId,
                    GroupName = s.GroupName,
                    Status = s.Status ?? 1,
                    VenueData = new VenueDataDto(
                        s.Venue!.GooglePlaceId,
                        s.Venue.VenueName,
                        s.Venue.Address ?? "",
                        s.Venue.Latitude ?? 0,
                        s.Venue.Longitude ?? 0
                    ),
                    SessionDate = s.SessionDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    GameTypeId = s.GameTypeId,
                    PairingMethodId = s.PairingMethodId,
                    MaxParticipants = s.MaxParticipants,
                    CostingMethod = s.CostingMethod,
                    CourtFeePerPerson = s.CourtFeePerPerson,
                    ShuttlecockFeePerPerson = s.ShuttlecockFeePerPerson,
                    TotalCourtCost = s.TotalCourtCost,
                    ShuttlecockCostPerUnit = s.ShuttlecockCostPerUnit,
                    ShuttlecockModelId = s.ShuttlecockModelId,
                    ShuttlecockBrandId = s.ShuttlecockModel != null ? s.ShuttlecockModel.BrandId : null, // <-- เพิ่ม BrandId
                    ShuttlecockBrandName = s.ShuttlecockModel != null ? s.ShuttlecockModel.Brand.BrandName : null, // Map BrandName
                    ShuttlecockModelName = s.ShuttlecockModel != null ? s.ShuttlecockModel.ModelName : null, // Map ModelName
                    GameTypeName = s.GameType != null ? s.GameType.TypeName : null, // Map GameTypeName
                    NumberOfCourts = s.NumberOfCourts,
                    CourtNumbers = s.CourtNumbers,
                    Notes = s.Notes,
                    FacilityIds = s.GameSessionFacilities.Select(f => f.FacilityId).ToList(),
                    PhotoUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
                })
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return null;
            }

            // --- NEW: คำนวณจำนวนเกมที่เล่นจบแล้วของทุกคน (เหมือนข้างบน) ---
            var finishedMatchPlayers = await _context.MatchPlayers
                .Where(mp => mp.Match.SessionId == sessionId && mp.Match.Status == 2)
                .Select(mp => new { mp.UserId, mp.WalkinId })
                .ToListAsync();

            var memberGameCounts = finishedMatchPlayers
                .Where(mp => mp.UserId.HasValue)
                .GroupBy(mp => mp.UserId)
                .ToDictionary(g => g.Key!.Value, g => g.Count());

            var guestGameCounts = finishedMatchPlayers
                .Where(mp => mp.WalkinId.HasValue)
                .GroupBy(mp => mp.WalkinId)
                .ToDictionary(g => g.Key!.Value, g => g.Count());
            // ---------------------------------------------------

            var registeredParticipants = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId)
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .ToListAsync();

            var walkinGuests = await _context.SessionWalkinGuests
                .Where(g => g.SessionId == sessionId)
                .Include(g => g.SkillLevel)
                .ToListAsync();

            session.Participants.AddRange(registeredParticipants.Select(p => ParticipantDtoMapper.FromMember(p, memberGameCounts.ContainsKey(p.UserId) ? memberGameCounts[p.UserId] : 0)));
            session.Participants.AddRange(walkinGuests.Select(g => ParticipantDtoMapper.FromGuest(g, guestGameCounts.ContainsKey(g.WalkinId) ? guestGameCounts[g.WalkinId] : 0)));

            session.Participants = session.Participants.OrderBy(p => p.Status).ThenBy(p => p.ParticipantId).ToList();
            session.CurrentParticipants = session.Participants.Count(p => p.Status == 1); // คำนวณจำนวนผู้เล่น

            return session;
        }

        public async Task<IEnumerable<UpcomingSessionCardDto>> GetUpcomingSessionsAsync(int? currentUserId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var thaiCulture = new CultureInfo("th-TH");
            var userBookmarks = new HashSet<int>(); // Placeholder

            return await _context.GameSessions
                .Where(s => s.SessionDate >= today && s.Status == 1)
                .Include(s => s.Venue)
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.CreatedByUser.UserProfile)
                .Include(s => s.SessionParticipants)
                .Include(s => s.GameType)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .Select(s => new UpcomingSessionCardDto
                {
                    SessionPublicId = s.SessionPublicId,
                    SessionId = s.SessionId,
                    GroupName = s.GroupName, // << เพิ่มกลับเข้ามา
                    ImageUrl = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).FirstOrDefault(),
                    DayOfWeek = s.SessionDate.ToDateTime(TimeOnly.MinValue).ToString("dddd", thaiCulture),
                    SessionDate = s.SessionDate.ToString("dd/MM/yyyy", thaiCulture),
                    StartTime = s.StartTime.ToString("HH:mm"),
                    EndTime = s.EndTime.ToString("HH:mm"),
                    SessionStart = s.SessionDate.ToDateTime(s.StartTime), // << เพิ่มกลับเข้ามา
                    CourtName = s.Venue.VenueName, // << เพิ่มกลับเข้ามา (ใช้ VenueName)
                    Location = s.Venue.Address,
                    Latitude = s.Venue.Latitude,
                    Longitude = s.Venue.Longitude,
                    Price = (s.CourtFeePerPerson.HasValue || s.ShuttlecockFeePerPerson.HasValue)
                          ? $"{(s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0):N0} บาท"
                          : "สอบถามผู้จัด",
                    OrganizerName = s.CreatedByUser!.UserProfile!.Nickname ?? "N/A",
                    OrganizerImageUrl = s.CreatedByUser.UserProfile.ProfilePhotoUrl,
                    IsBookmarked = userBookmarks.Contains(s.SessionId),
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1) + s.SessionWalkinGuests.Count(g => g.Status == 1),
                    GameTypeName = s.GameType!.TypeName,
                    ShuttlecockBrandName = s.ShuttlecockModel!.Brand!.BrandName,
                    ShuttlecockModelName = s.ShuttlecockModel.ModelName,
                    CourtImageUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
                    Status = s.Status,
                    CourtNumbers = s.CourtNumbers,
                    Notes = s.Notes,
                })
                .ToListAsync();
        }


        public async Task<ManageGameSessionDto?> UpdateSessionAsync(int sessionId, int organizerUserId, SaveGameSessionDto dto)
        {
            if (dto.StartTime >= dto.EndTime)
            {
                throw new Exception("เวลาเริ่มต้น ต้องน้อยกว่า เวลาสิ้นสุด");
            }

            // --- 1. เช็คเวลาและสนามทับซ้อนก่อนแก้ไข ---
            var overlappingSession = await _context.GameSessions
                .Include(s => s.Venue)
                .Where(s => s.CreatedByUserId == organizerUserId
                         && s.SessionId != sessionId // ยกเว้นก๊วนที่กำลังแก้ไขอยู่
                         && s.Status != 3 && s.Status != 4 // ไม่เช็คก๊วนที่ยกเลิกหรือจบไปแล้ว
                         && s.SessionDate == dto.SessionDate
                         && s.Venue.GooglePlaceId == dto.VenueData.GooglePlaceId
                         && s.StartTime < dto.EndTime
                         && s.EndTime > dto.StartTime)
                .FirstOrDefaultAsync();

            if (overlappingSession != null)
            {
                string timeOld = $"{overlappingSession.StartTime:hh\\:mm} - {overlappingSession.EndTime:hh\\:mm}";
                throw new Exception($"พบเวลาทับซ้อน!\nก๊วนนี้เวลาทับกับ \"{overlappingSession.GroupName}\"\nที่สนามนี้ในเวลา {timeOld}\nกรุณาตรวจสอบเวลาอีกครั้ง");
            }

            var sessionToUpdate = await _context.GameSessions
                .Include(s => s.GameSessionFacilities)
                .Include(s => s.GameSessionPhotos)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (sessionToUpdate == null)
            {
                return null;
            }

            int venueId;
            var existingVenue = await _context.Venues
                .FirstOrDefaultAsync(v => v.GooglePlaceId == dto.VenueData.GooglePlaceId);

            if (existingVenue != null)
            {
                venueId = existingVenue.VenueId;
            }
            else
            {
                var newVenue = new Venue
                {
                    GooglePlaceId = dto.VenueData.GooglePlaceId,
                    VenueName = dto.VenueData.Name,
                    Address = dto.VenueData.Address,
                    Latitude = dto.VenueData.Latitude,
                    Longitude = dto.VenueData.Longitude,
                    FirstUsedByUserId = organizerUserId
                };
                await _context.Venues.AddAsync(newVenue);
                await _context.SaveChangesAsync();
                venueId = newVenue.VenueId;
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // --- NEW: เปรียบเทียบข้อมูลเพื่อแจ้งเตือนว่าแก้อะไรไปบ้าง ---
                    var changes = new List<string>();
                    var thaiCulture = new CultureInfo("th-TH");
                    if (sessionToUpdate.SessionDate != dto.SessionDate) changes.Add($"วันที่เป็น {dto.SessionDate.ToString("dd/MM/yyyy", thaiCulture)}");
                    if (sessionToUpdate.StartTime != dto.StartTime || sessionToUpdate.EndTime != dto.EndTime) changes.Add($"เวลาเป็น {dto.StartTime:HH\\:mm}-{dto.EndTime:HH\\:mm} น.");
                    if (sessionToUpdate.CourtFeePerPerson != dto.CourtFeePerPerson) changes.Add($"ค่าสนามเป็น {dto.CourtFeePerPerson} บ.");
                    if (sessionToUpdate.ShuttlecockFeePerPerson != dto.ShuttlecockFeePerPerson) changes.Add($"ค่าลูกแบดเป็น {dto.ShuttlecockFeePerPerson} บ.");
                    if (sessionToUpdate.MaxParticipants != dto.MaxParticipants) changes.Add($"จำนวนรับเป็น {dto.MaxParticipants} คน");

                    string changeMessage = changes.Any() 
                        ? $"ก๊วน '{sessionToUpdate.GroupName}' มีการแก้: " + string.Join(", ", changes)
                        : $"ข้อมูลก๊วน '{sessionToUpdate.GroupName}' มีการอัปเดต กรุณาตรวจสอบรายละเอียด";

                    sessionToUpdate.GroupName = dto.GroupName;
                    sessionToUpdate.VenueId = venueId;
                    sessionToUpdate.SessionDate = dto.SessionDate;
                    sessionToUpdate.StartTime = dto.StartTime;
                    sessionToUpdate.EndTime = dto.EndTime;
                    sessionToUpdate.MaxParticipants = dto.MaxParticipants;
                    sessionToUpdate.GameTypeId = dto.GameTypeId;
                    sessionToUpdate.PairingMethodId = dto.PairingMethodId;
                    sessionToUpdate.CostingMethod = (byte?)dto.CostingMethod;
                    sessionToUpdate.CourtFeePerPerson = dto.CourtFeePerPerson;
                    sessionToUpdate.ShuttlecockFeePerPerson = dto.ShuttlecockFeePerPerson;
                    sessionToUpdate.TotalCourtCost = dto.TotalCourtCost;
                    sessionToUpdate.ShuttlecockCostPerUnit = dto.ShuttlecockCostPerUnit;
                    sessionToUpdate.ShuttlecockModelId = dto.ShuttlecockModelId;
                    sessionToUpdate.NumberOfCourts = dto.NumberOfCourts;
                    sessionToUpdate.CourtNumbers = dto.CourtNumbers;
                    sessionToUpdate.Notes = dto.Notes;
                    sessionToUpdate.UpdatedDate = DateTime.UtcNow;

                    if (dto.FacilityIds != null)
                    {
                        _context.GameSessionFacilities.RemoveRange(sessionToUpdate.GameSessionFacilities);
                        if (dto.FacilityIds.Any())
                        {
                            var newFacilities = dto.FacilityIds.Select(id => new GameSessionFacility
                            {
                                SessionId = sessionId,
                                FacilityId = id,
                                CreatedBy = organizerUserId
                            });
                            await _context.GameSessionFacilities.AddRangeAsync(newFacilities);
                        }
                    }


                    if (dto.PhotoUrls != null)
                    {
                        _context.GameSessionPhotos.RemoveRange(sessionToUpdate.GameSessionPhotos);
                        if (dto.PhotoUrls.Any())
                        {
                            var newPhotos = dto.PhotoUrls.Select((url, i) => new GameSessionPhoto
                            {
                                SessionId = sessionId,
                                PhotoUrl = url,
                                DisplayOrder = (byte)(i + 1),
                                CreatedBy = organizerUserId
                            });
                            await _context.GameSessionPhotos.AddRangeAsync(newPhotos);
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // --- แจ้งเตือนผู้เล่นทุกคนในก๊วนเกี่ยวกับการอัปเดต ---
                    var participantUserIds = await _context.SessionParticipants
                        .Where(p => p.SessionId == sessionId && p.Status != 3)
                        .Select(p => p.UserId)
                        .ToListAsync();

                    foreach (var userId in participantUserIds)
                    {
                        await _notificationService.SendNotificationAsync(
                            userId,
                            "ข้อมูลก๊วนมีการเปลี่ยนแปลง",
                            changeMessage,
                            "SESSION_UPDATED",
                            sessionId
                        );
                    }
                    return await GetSessionForManageViewAsync(sessionId, organizerUserId);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }


        public async Task<bool> CancelSessionAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);
            if (session == null) return false;

            session.Status = 3;
            session.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CancelSessionByOrganizerAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions
                .Include(s => s.ParticipantBills).ThenInclude(b => b.BillLineItems) // FIX: ต้อง Include BillLineItems มาด้วย ไม่งั้น serviceFeeItem จะหา fee เจอเป็น null → คำนวณคืนเงินผู้จัดผิด
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null) return false;

            session.Status = 3; // 3 = Cancelled
            session.UpdatedDate = DateTime.UtcNow;

            // --- NEW: ระบบคืนเงินเข้า Wallet อัตโนมัติ (กรณีผู้จัดยกเลิก คืนเต็มจำนวน) ---
            var paidBills = session.ParticipantBills.Where(b => b.Status == 2).ToList();
            
            foreach (var bill in paidBills)
            {
                if (bill.UserId.HasValue && bill.TotalAmount > 0)
                {
                    int refundUserId = bill.UserId.Value;
                    decimal refundAmount = bill.TotalAmount; // คืนเต็มจำนวน

                    // 1. หา Wallet ของ User (ถ้าไม่มีให้สร้างใหม่)
                    var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == refundUserId);
                    if (wallet == null)
                    {
                        wallet = new UserWallet { UserId = refundUserId, Balance = 0 };
                        await _context.UserWallets.AddAsync(wallet);
                    }

                    // 2. เติมเงินเข้า Wallet
                    wallet.Balance += refundAmount;
                    wallet.UpdatedDate = DateTime.UtcNow;

                    // --- FIX: ดึงเงินกลับจาก Wallet ผู้จัด (ยอมให้ติดลบได้) ---
                    var serviceFeeItem = bill.BillLineItems.FirstOrDefault(li => li.Description == "ค่าธรรมเนียม");
                    decimal serviceFee = serviceFeeItem?.Amount ?? 0;
                    decimal amountToDeductFromOrg = refundAmount - serviceFee; // ดึงกลับเฉพาะส่วนที่ผู้จัดได้ไป

                    if (amountToDeductFromOrg > 0)
                    {
                        var orgWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == organizerUserId);
                        if (orgWallet == null)
                        {
                            orgWallet = new UserWallet { UserId = organizerUserId, Balance = 0 };
                            await _context.UserWallets.AddAsync(orgWallet);
                        }
                        orgWallet.Balance -= amountToDeductFromOrg; // ยอมให้ติดลบ
                        orgWallet.UpdatedDate = DateTime.UtcNow;
                        await _context.WalletTransactions.AddAsync(new WalletTransaction { Wallet = orgWallet, Amount = amountToDeductFromOrg, TransactionType = 2, Description = $"หักเงินคืนผู้เล่น (ยกเลิกก๊วน): {session.GroupName}", ReferenceId = sessionId });
                    }
                    // ----------------------------------------------------

                    // 3. สร้างประวัติ Transaction
                    var transaction = new WalletTransaction
                    {
                        Wallet = wallet, // ใช้ Navigation Property เพื่อให้ EF ผูก ID ให้อัตโนมัติ
                        Amount = refundAmount,
                        TransactionType = 1, // 1 = IN (Refund)
                        Description = $"คืนเงินกรณียกเลิกก๊วน: {session.GroupName}",
                        ReferenceId = sessionId,
                    };
                    await _context.WalletTransactions.AddAsync(transaction);

                    // 4. เปลี่ยนสถานะบิลเป็น 3 (Cancelled) เพื่อล้างยอดออกจากระบบบัญชี
                    bill.Status = 3;
                }
            }

            // --- แจ้งเตือนผู้เล่นทุกคนในก๊วนเกี่ยวกับการยกเลิก ---
            var participantUserIds = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId && p.Status != 3)
                .Select(p => p.UserId)
                .ToListAsync();

            foreach (var userId in participantUserIds)
            {
                await _notificationService.SendNotificationAsync(
                    userId,
                    "ก๊วนถูกยกเลิก",
                    $"ก๊วน '{session.GroupName}' ที่คุณเข้าร่วมได้ถูกยกเลิกโดยผู้จัด",
                    "SESSION_CANCELLED",
                    sessionId
                );
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ManageGameSessionDto> DuplicateSessionForNextWeekAsync(int oldSessionId, int organizerUserId)
        {
            var oldSession = await _context.GameSessions
                .Include(s => s.GameSessionFacilities)
                .Include(s => s.Venue)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == oldSessionId && s.CreatedByUserId == organizerUserId);

            if (oldSession == null) throw new KeyNotFoundException("Session not found or you do not own this session.");

            var venueData = new VenueDataDto(
                oldSession.Venue.GooglePlaceId,
                oldSession.Venue.VenueName,
                oldSession.Venue.Address ?? "",
                oldSession.Venue.Latitude ?? 0,
                oldSession.Venue.Longitude ?? 0
                );

            var dto = new SaveGameSessionDto(
                oldSession.GroupName,
                venueData,
                oldSession.SessionDate.AddDays(7),
                oldSession.StartTime,
                oldSession.EndTime,
                oldSession.GameTypeId,
                oldSession.PairingMethodId,
                oldSession.MaxParticipants,
                oldSession.CostingMethod,
                oldSession.CourtFeePerPerson,
                oldSession.ShuttlecockFeePerPerson,
                oldSession.TotalCourtCost,
                oldSession.ShuttlecockCostPerUnit,
                oldSession.ShuttlecockModelId,
                oldSession.NumberOfCourts,
                oldSession.CourtNumbers,
                oldSession.Notes,
                oldSession.GameSessionFacilities.Select(f => f.FacilityId).ToList(),
                new List<string>() // ไม่คัดลอกรูป
            );

            return await CreateSessionAsync(organizerUserId, dto);
        }

        public Task<(ParticipantDto? Data, string ErrorMessage)> AddGuestAsync(int sessionId, int organizerUserId, AddGuestDto dto)
            => _bookingService.AddGuestAsync(sessionId, organizerUserId, dto);

        public async Task<(bool Success, string ErrorMessage)> UpdateParticipantSkillLevelAsync(int sessionId, string participantType, int participantId, int? newSkillLevelId, int organizerUserId)
        {
            var session = await _context.GameSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null)
            {
                return (false, "Session not found or you do not have permission.");
            }

            if (participantType.Equals(ParticipantTypes.Member, StringComparison.OrdinalIgnoreCase))
            {
                var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.ParticipantId == participantId && p.SessionId == sessionId);
                if (participant == null)
                {
                    return (false, "Participant not found in this session.");
                }
                participant.SkillLevelId = newSkillLevelId;
            }
            else if (participantType.Equals(ParticipantTypes.Guest, StringComparison.OrdinalIgnoreCase))
            {
                var guest = await _context.SessionWalkinGuests.FirstOrDefaultAsync(g => g.WalkinId == participantId && g.SessionId == sessionId);
                if (guest == null)
                {
                    return (false, "Guest participant not found in this session.");
                }
                guest.SkillLevelId = newSkillLevelId;
            }
            else
            {
                return (false, "Invalid participant type. Must be 'Member' or 'Guest'.");
            }

            await _context.SaveChangesAsync();

            return (true, "Skill level updated successfully.");
        }


        public async Task<IEnumerable<UpcomingSessionCardDto>> GetMyUpcomingSessionsAsync(int organizerUserId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var thaiCulture = new CultureInfo("th-TH");

            // 1. โหลดข้อมูลทั้งหมดมาเก็บใน Memory ก่อน (เพื่อเลี่ยงปัญหา EF Core แปลง C# เป็น SQL ไม่ได้)
            var rawSessions = await _context.GameSessions
                .Where(s => s.CreatedByUserId == organizerUserId && s.SessionDate >= today && (s.Status == 1 || s.Status == 2))
                .Include(s => s.Venue)
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.CreatedByUser).ThenInclude(u => u.UserProfile)
                .Include(s => s.SessionParticipants).ThenInclude(p => p.User).ThenInclude(u => u.UserProfile)
                .Include(s => s.SessionParticipants).ThenInclude(p => p.SkillLevel)
                .Include(s => s.SessionWalkinGuests).ThenInclude(g => g.SkillLevel)
                .Include(s => s.ParticipantBills).ThenInclude(b => b.BillLineItems)
                .Include(s => s.GameType)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand)
                .Include(s => s.GameSessionFacilities).ThenInclude(f => f.Facility)
                .AsSplitQuery()
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .ToListAsync();

            var result = new List<UpcomingSessionCardDto>();
            
            foreach (var s in rawSessions)
            {
                var sessionStartDt = s.SessionDate.ToDateTime(s.StartTime);
                
                var dto = new UpcomingSessionCardDto
                {
                    SessionPublicId = s.SessionPublicId,
                    SessionId = s.SessionId,
                    GroupName = s.GroupName,
                    ImageUrl = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).FirstOrDefault(),
                    DayOfWeek = sessionStartDt.ToString("dddd", thaiCulture),
                    SessionDate = sessionStartDt.ToString("dd/MM/yyyy", thaiCulture),
                    StartTime = s.StartTime.ToString("HH:mm"),
                    EndTime = s.EndTime.ToString("HH:mm"),
                    SessionStart = sessionStartDt,
                    CourtName = s.Venue.VenueName,
                    Location = s.Venue.Address,
                    Latitude = s.Venue.Latitude,
                    Longitude = s.Venue.Longitude,
                    Price = (s.CourtFeePerPerson.HasValue || s.ShuttlecockFeePerPerson.HasValue)
                          ? $"{(s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0):N0} บาท"
                          : "สอบถามผู้จัด",
                    CourtFeePerPerson = s.CourtFeePerPerson.ToString(),
                    ShuttlecockFeePerPerson = s.ShuttlecockFeePerPerson.ToString(),
                    OrganizerName = s.CreatedByUser?.UserProfile?.Nickname ?? "N/A",
                    OrganizerImageUrl = s.CreatedByUser?.UserProfile?.ProfilePhotoUrl,
                    IsBookmarked = false,
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1) + s.SessionWalkinGuests.Count(g => g.Status == 1),
                    GameTypeName = s.GameType?.TypeName,
                    ShuttlecockBrandName = s.ShuttlecockModel?.Brand?.BrandName,
                    ShuttlecockModelName = s.ShuttlecockModel?.ModelName,
                    CourtImageUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
                    Status = s.Status,
                    CourtNumbers = s.CourtNumbers,
                    // Smart Backend: ตรวจสอบเวลาโดยอิงจากเวลาประเทศไทย (UTC+7)
                    CanStartSession = (sessionStartDt - DateTime.UtcNow.AddHours(7)).TotalMinutes <= 180,
                    Notes = s.Notes,
                    PaidAmount = s.ParticipantBills.Where(b => b.Status == 2).SelectMany(b => b.BillLineItems).Where(li => li.Description != "ค่าธรรมเนียม").Sum(li => li.Amount),
                    TotalIncome = (s.SessionParticipants.Count(p => p.Status == 1) + s.SessionWalkinGuests.Count(g => g.Status == 1)) * ((s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0)),
                    
                    Facilities = s.GameSessionFacilities.Where(f => f.Facility != null)
                        .Select(f => new FacilityDto(f.FacilityId, f.Facility!.FacilityName, f.Facility.IconUrl ?? "")).ToList(),
                    Participants = new List<ParticipantDto>()
                };

                var activeMembers = s.SessionParticipants.Where(p => p.Status != 3).Select(p => ParticipantDtoMapper.FromMember(p)).ToList();
                var activeGuests = s.SessionWalkinGuests.Where(g => g.Status != 3).Select(g => ParticipantDtoMapper.FromGuest(g)).ToList();

                dto.Participants = activeMembers.Concat(activeGuests).OrderBy(p => p.Status).ThenBy(p => p.ParticipantId).ToList();

                result.Add(dto);
            }

            return result;
        }

        public async Task<IEnumerable<OrganizerGameSessionDto>> GetMyPastSessionsAsync(int organizerUserId, string? keyword = null, string? timeRange = null, int page = 1, int limit = 10)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            decimal serviceFee = _configuration.GetValue<decimal>("ServiceFee");

            IQueryable<GameSession> query = _context.GameSessions
               .Where(s => s.CreatedByUserId == organizerUserId && (s.SessionDate < today || s.Status == 3 || s.Status == 4)); // กรองเฉพาะอดีต, ยกเลิก(3), หรือก๊วนที่กดจบแล้ว(4)

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lowerKeyword = keyword.ToLower();
                bool isDateSearch = DateTime.TryParse(keyword, out DateTime parsedDate);
                DateOnly searchDate = isDateSearch ? DateOnly.FromDateTime(parsedDate) : default;

                query = query.Where(s => 
                    s.GroupName.ToLower().Contains(lowerKeyword) ||
                    (s.Venue != null && s.Venue.VenueName.ToLower().Contains(lowerKeyword)) ||
                    (isDateSearch && s.SessionDate == searchDate)
                );
            }
            
            // --- NEW: กรองข้อมูลตามช่วงเวลา (Smart Backend) ---
            if (!string.IsNullOrWhiteSpace(timeRange) && timeRange != "ทั้งหมด")
            {
                if (timeRange == "วันนี้")
                {
                    query = query.Where(s => s.SessionDate == today);
                }
                else if (timeRange == "สัปดาห์นี้")
                {
                    int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    var startOfWeek = today.AddDays(-1 * diff);
                    var endOfWeek = startOfWeek.AddDays(6);
                    query = query.Where(s => s.SessionDate >= startOfWeek && s.SessionDate <= endOfWeek);
                }
                else if (timeRange == "เดือนนี้")
                {
                    query = query.Where(s => s.SessionDate.Month == today.Month && s.SessionDate.Year == today.Year);
                }
            }

            var sessions = await query
               .Include(s => s.Venue)
               .Include(s => s.SessionParticipants)
               .Include(s => s.SessionWalkinGuests)
               .Include(s => s.ParticipantBills).ThenInclude(b => b.BillLineItems) // FIX: Include LineItems เพื่อหักค่าธรรมเนียม
               .Include(s => s.Matches).ThenInclude(m => m.MatchPlayers)
               .AsSplitQuery() // FIX: ป้องกันปัญหา Cartesian Explosion
               .OrderByDescending(s => s.SessionDate) // เรียงจากล่าสุดไปเก่าสุด
               .ThenByDescending(s => s.StartTime)
               .Skip((page - 1) * limit).Take(limit)
               .ToListAsync();

            var result = new List<OrganizerGameSessionDto>();

            foreach (var s in sessions)
            {
                var activeMembers = s.SessionParticipants.Where(p => p.Status == 1).ToList();
                var activeGuests = s.SessionWalkinGuests.Where(g => g.Status == 1).ToList();
                int totalPlayers = activeMembers.Count + activeGuests.Count;

                decimal courtFeePerPerson = s.CourtFeePerPerson ?? 0;
                decimal shuttleFeePerPerson = s.ShuttlecockFeePerPerson ?? 0;

                int CountGames(int? userId, int? walkinId)
                {
                    return s.Matches.Count(m => m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == userId && mp.WalkinId == walkinId));
                }

                (decimal paid, decimal total) CalculateParticipantFinancials(int? userId, int? walkinId, int gamesPlayed)
                {
                    var bills = s.ParticipantBills.Where(b => b.UserId == userId && b.WalkinId == walkinId && b.Status != 3).ToList();
                    
                    // FIX: ป้องกันการนำบิลค้างชำระ (Status=1) มาบวกซ้ำกับบิลที่จ่ายแล้ว (Status=2)
                    var activeBills = bills.Where(b => b.Status == 2).ToList();
                    if (!activeBills.Any()) 
                    {
                        var latestPending = bills.Where(b => b.Status == 1).OrderByDescending(b => b.CreatedDate).FirstOrDefault();
                        if (latestPending != null) activeBills.Add(latestPending);
                    }

                    decimal cPart = courtFeePerPerson;
                    decimal sPart = s.CostingMethod == 2 ? shuttleFeePerPerson : shuttleFeePerPerson * gamesPlayed;
                    decimal customItems = 0;

                    if (activeBills.Any())
                    {
                        cPart = activeBills.SelectMany(b => b.BillLineItems).Where(li => li.Description == "ค่าสนาม" || li.Description == "ค่าคอร์ท").Sum(li => li.Amount);
                        if (cPart == 0 && courtFeePerPerson > 0) cPart = courtFeePerPerson;

                        sPart = activeBills.SelectMany(b => b.BillLineItems).Where(li => li.Description.StartsWith("ค่าลูกแบด")).Sum(li => li.Amount);
                        if (sPart == 0) sPart = s.CostingMethod == 2 ? shuttleFeePerPerson : shuttleFeePerPerson * gamesPlayed;

                        customItems = activeBills.SelectMany(b => b.BillLineItems).Where(li => li.Description != "ค่าสนาม" && li.Description != "ค่าคอร์ท" && li.Description != "ค่าธรรมเนียม" && !li.Description.StartsWith("ค่าลูกแบด")).Sum(li => li.Amount);
                    }

                    // หักลบค่าธรรมเนียมแอปออก เพื่อให้แสดงเฉพาะรายรับของผู้จัดจริงๆ
                    decimal serviceFeeTotal = activeBills.SelectMany(b => b.BillLineItems).Where(li => li.Description == "ค่าธรรมเนียม").Sum(li => li.Amount);
                    decimal serviceFeePaid = bills.Where(b => b.Status == 2).SelectMany(b => b.BillLineItems).Where(li => li.Description == "ค่าธรรมเนียม").Sum(li => li.Amount);

                    decimal paidVal = bills.Where(b => b.Status == 2).Sum(b => b.TotalAmount) - serviceFeePaid;
                    if (paidVal < 0) paidVal = 0;

                    decimal billedTotal = activeBills.Sum(b => b.TotalAmount) - serviceFeeTotal;
                    if (billedTotal < 0) billedTotal = 0;

                    decimal totalVal = cPart + sPart + customItems;
                    if (billedTotal > totalVal) totalVal = billedTotal;

                    return (paidVal, totalVal);
                }

                decimal aggTotalIncome = 0;
                decimal aggPaidAmount = 0;
                decimal aggUnpaidAmount = 0;

                foreach (var m in activeMembers)
                {
                    int games = CountGames(m.UserId, null);
                    var (paid, total) = CalculateParticipantFinancials(m.UserId, null, games);
                    aggTotalIncome += total;
                    aggPaidAmount += paid;
                    aggUnpaidAmount += (total - paid > 0 ? total - paid : 0);
                }

                foreach (var g in activeGuests)
                {
                    int games = CountGames(null, g.WalkinId);
                    var (paid, total) = CalculateParticipantFinancials(null, g.WalkinId, games);
                    aggTotalIncome += total;
                    aggPaidAmount += paid;
                    aggUnpaidAmount += (total - paid > 0 ? total - paid : 0);
                }

                decimal feePerPerson = (s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0);

                result.Add(new OrganizerGameSessionDto
                {
                    GameSessionId = s.SessionId,
                    Date = s.SessionDate.ToDateTime(s.StartTime),
                    GroupName = s.GroupName,
                    TotalIncome = aggTotalIncome,
                    PaidAmount = aggPaidAmount,
                    UnpaidAmount = aggUnpaidAmount,
                    Status = s.Status == 3 ? "Cancelled" : (s.Status == 4 || s.SessionDate < today ? "Ended" : (s.Status == 2 ? "Started" : "Open")),
                    StartTime = s.StartTime.ToString("HH:mm"),
                    EndTime = s.EndTime.ToString("HH:mm"),
                    TotalParticipants = totalPlayers,
                    TotalCourts = s.NumberOfCourts,
                    VenueName = s.Venue.VenueName,
                    Price = feePerPerson
                });
            }

            return result;
        }

        public async Task<GameSessionAnalyticsDto?> GetSessionAnalyticsAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null) return null;

            // ดึงข้อมูลแมตช์ที่จบแล้ว (Status = 2)
            var matches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 2)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User!).ThenInclude(u => u.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin)
                .OrderBy(m => m.StartTime)
                .ToListAsync();

            var analytics = new GameSessionAnalyticsDto
            {
                GroupName = session.GroupName,
                Date = session.SessionDate.ToDateTime(session.StartTime),
                TotalGames = matches.Count,
                TotalShuttlecocks = matches.Sum(m => m.ShuttlecocksUsed)
            };

            if (matches.Any())
            {
                var firstMatch = matches.First();
                var lastMatch = matches.Last();

                analytics.TotalPlayTimeStart = firstMatch.StartTime?.ToString("HH:mm") ?? "-";
                analytics.TotalPlayTimeEnd = lastMatch.EndTime?.ToString("HH:mm") ?? "-";

                // คำนวณระยะเวลา
                var durations = matches
                    .Where(m => m.StartTime.HasValue && m.EndTime.HasValue)
                    .Select(m => new
                    {
                        Match = m,
                        Duration = (m.EndTime!.Value - m.StartTime!.Value)
                    })
                    .ToList();

                if (durations.Any())
               {
                    var avgSeconds = durations.Average(d => d.Duration.TotalSeconds);
                    analytics.AveragePlayTimePerGame = TimeSpan.FromSeconds(avgSeconds).ToString(@"mm\:ss");

                    var longest = durations.OrderByDescending(d => d.Duration).First();
                    var shortest = durations.OrderBy(d => d.Duration).First();

                    Func<Match, string> getPlayerNames = (m) =>
                    {
                        var teamA = string.Join(", ", m.MatchPlayers.Where(p => p.Team == "A").Select(p => p.User?.UserProfile?.Nickname ?? p.Walkin?.GuestName ?? "N/A"));
                        var teamB = string.Join(", ", m.MatchPlayers.Where(p => p.Team == "B").Select(p => p.User?.UserProfile?.Nickname ?? p.Walkin?.GuestName ?? "N/A"));
                        return $"{teamA} vs {teamB}";
                    };

                    analytics.LongestGame = new MatchPerformanceDto { Players = getPlayerNames(longest.Match), Duration = longest.Duration.ToString(@"mm\:ss") + " นาที" };
                    analytics.ShortestGame = new MatchPerformanceDto { Players = getPlayerNames(shortest.Match), Duration = shortest.Duration.ToString(@"mm\:ss") + " นาที" };
                }

                // สร้าง Match History List
                analytics.MatchHistory = matches.Select(m => new MatchHistoryDto
                {
                    MatchId = m.MatchId,
                    CourtNumber = m.CourtNumber ?? "-",
                    ShuttlecocksUsed = m.ShuttlecocksUsed,
                    TeamA = string.Join(", ", m.MatchPlayers.Where(p => p.Team == "A").Select(p => p.User?.UserProfile?.Nickname ?? p.Walkin?.GuestName ?? "N/A")),
                    TeamB = string.Join(", ", m.MatchPlayers.Where(p => p.Team == "B").Select(p => p.User?.UserProfile?.Nickname ?? p.Walkin?.GuestName ?? "N/A")),
                    Duration = (m.StartTime.HasValue && m.EndTime.HasValue) ? (m.EndTime.Value - m.StartTime.Value).ToString(@"mm\:ss") : "-"
                }).ToList();
            }

            return analytics;
        }

        public async Task<(bool Success, string ErrorMessage)> StartSessionAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null)
            {
                return (false, "Session not found or you do not have permission.");
            }

            // (ทางเลือก) อาจจะเพิ่มเงื่อนไขว่าต้องเป็นวันเดียวกับที่จัด ถึงจะเปิดได้
            if (session.SessionDate != DateOnly.FromDateTime(DateTime.Now))
            {
               return (false, "You can only start the session on the day of the event.");
            }

            if (session.Status == 3 || session.Status == 2)
            {
                return (false, "This session is already cancelled or has already started.");
            }

            session.Status = 2;
            session.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, "Session started successfully.");
        }

        public Task<GameSessionFinancialsDto?> GetSessionFinancialsAsync(int sessionId, int organizerUserId)
            => _billingService.GetSessionFinancialsAsync(sessionId, organizerUserId);

        public async Task<bool> StartCompetitionAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);
            if (session == null) return false;

            // บันทึกเวลาเริ่ม ถ้ายังไม่เคยเริ่ม
            if (session.CompetitionStartTime == null)
            {
                session.CompetitionStartTime = DateTime.UtcNow;
            }
            
            // อัปเดตสถานะเป็น Started (2) ด้วยเพื่อให้สอดคล้องกัน
            if (session.Status == 1) session.Status = 2;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> EndCompetitionAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions
                .Include(s => s.Matches) // Include Matches เพื่อดึงรายการแข่งมาเช็ค
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);
            
            if (session == null) return false;

            // --- NEW: จบแมตช์ที่ค้างอยู่ทั้งหมด (Status 1 -> 2) เพื่อให้คิดเงินได้ครบ ---
            var activeMatches = session.Matches.Where(m => m.Status == 1).ToList();
            foreach (var match in activeMatches)
            {
                match.Status = 2; // 2 = Ended
                match.EndTime = DateTime.UtcNow;
            }

            session.CompetitionEndTime = DateTime.UtcNow;
            session.Status = 4; // กำหนดสถานะเป็น 4 (จบการแข่งขัน)
            await _context.SaveChangesAsync();

            // --- 🔔 แจ้งเตือนผู้เล่นในก๊วน ว่าจบการแข่งขันแล้ว ---
            var participantIds = await _context.SessionParticipants.Where(p => p.SessionId == sessionId && (p.Status == 1 || p.Status == 2)).Select(p => p.UserId).ToListAsync();
            foreach (var pId in participantIds)
            {
                await _notificationService.SendNotificationAsync(pId, "จบก๊วนเรียบร้อย 🏸", $"ก๊วน '{session.GroupName}' แข่งขันเสร็จสิ้นแล้ว อย่าลืมชำระเงินและดูสถิติของคุณนะครับ!", "GAME_ENDED", sessionId);
            }

            return true;
        }

        public Task<(bool Success, string ErrorMessage)> RemoveParticipantAsync(int sessionId, string participantType, int participantId, int organizerUserId)
            => _bookingService.RemoveParticipantAsync(sessionId, participantType, participantId, organizerUserId);

        public Task<(bool Success, string ErrorMessage)> PromoteWaitlistedParticipantAsync(int sessionId, string participantType, int participantId, int organizerUserId)
            => _bookingService.PromoteWaitlistedParticipantAsync(sessionId, participantType, participantId, organizerUserId);

        public Task<(bool Success, string ErrorMessage)> AutoMatchAsync(int sessionId, int organizerUserId, AutoMatchRequestDto dto)
            => _autoMatchService.AutoMatchAsync(sessionId, organizerUserId, dto);

        public Task<(bool Success, string ErrorMessage)> SwapPlayersAsync(int sessionId, int organizerUserId, SwapPlayersRequestDto dto)
            => _autoMatchService.SwapPlayersAsync(sessionId, organizerUserId, dto);

        public Task<(bool Success, string ErrorMessage)> AssignReserveToCourtAsync(int sessionId, int organizerUserId, AssignReserveRequestDto dto)
            => _autoMatchService.AssignReserveToCourtAsync(sessionId, organizerUserId, dto);

        public Task<(bool Success, string ErrorMessage)> MovePlayersAsync(int sessionId, int organizerUserId, MovePlayersRequestDto dto)
            => _autoMatchService.MovePlayersAsync(sessionId, organizerUserId, dto);

        /// <summary>
        /// Background Job: สแกนก๊วนที่เลย EndTime แล้วแต่ผู้จัดยังไม่ได้กดเริ่ม (Status=1)
        /// ระบบจะ Auto-Cancel + คืนเงินผู้เล่นทุกคนอัตโนมัติ
        /// </summary>
        public async Task<int> AutoCancelExpiredSessionsAsync(CancellationToken ct = default)
        {
            // ใช้เวลาไทย (UTC+7) เทียบกับ SessionDate + EndTime
            var nowThai = DateTime.UtcNow.AddHours(7);
            var todayThai = DateOnly.FromDateTime(nowThai);
            var currentTimeThai = TimeOnly.FromDateTime(nowThai);

            // ค้นหาก๊วนที่:
            // 1. Status = 1 (Open, ยังไม่เริ่ม)
            // 2. วันจัดเป็นอดีต (SessionDate < วันนี้) หรือ วันนี้แต่เวลา EndTime ผ่านไปแล้ว
            var expiredSessions = await _context.GameSessions
                .Include(s => s.ParticipantBills).ThenInclude(b => b.BillLineItems)
                .Include(s => s.SessionParticipants)
                .Where(s => s.Status == 1 &&
                    (s.SessionDate < todayThai ||
                     (s.SessionDate == todayThai && s.EndTime <= currentTimeThai)))
                .ToListAsync(ct);

            int cancelledCount = 0;

            foreach (var session in expiredSessions)
            {
                try
                {
                    session.Status = 3; // Cancelled
                    session.UpdatedDate = DateTime.UtcNow;

                    // --- คืนเงินผู้เล่นทุกคนที่จ่ายแล้ว (เหมือน CancelSessionByOrganizerAsync) ---
                    var paidBills = session.ParticipantBills.Where(b => b.Status == 2).ToList();

                    foreach (var bill in paidBills)
                    {
                        if (bill.UserId.HasValue && bill.TotalAmount > 0)
                        {
                            int refundUserId = bill.UserId.Value;
                            decimal refundAmount = bill.TotalAmount;

                            // 1. คืนเงินเข้า Wallet ผู้เล่น
                            var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == refundUserId, ct);
                            if (wallet == null)
                            {
                                wallet = new UserWallet { UserId = refundUserId, Balance = 0 };
                                await _context.UserWallets.AddAsync(wallet, ct);
                            }
                            wallet.Balance += refundAmount;
                            wallet.UpdatedDate = DateTime.UtcNow;

                            // 2. ดึงเงินกลับจาก Wallet ผู้จัด (เฉพาะส่วนที่โอนให้ผู้จัดไป ไม่รวมค่าธรรมเนียม)
                            var serviceFeeItem = bill.BillLineItems.FirstOrDefault(li => li.Description == "ค่าธรรมเนียม");
                            decimal serviceFee = serviceFeeItem?.Amount ?? 0;
                            decimal amountToDeductFromOrg = refundAmount - serviceFee;

                            if (amountToDeductFromOrg > 0)
                            {
                                var orgWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == session.CreatedByUserId, ct);
                                if (orgWallet == null)
                                {
                                    orgWallet = new UserWallet { UserId = session.CreatedByUserId, Balance = 0 };
                                    await _context.UserWallets.AddAsync(orgWallet, ct);
                                }
                                orgWallet.Balance -= amountToDeductFromOrg;
                                orgWallet.UpdatedDate = DateTime.UtcNow;
                                await _context.WalletTransactions.AddAsync(new WalletTransaction
                                {
                                    Wallet = orgWallet,
                                    Amount = amountToDeductFromOrg,
                                    TransactionType = 2, // OUT
                                    Description = $"หักเงินคืนผู้เล่น (ก๊วนหมดเวลา): {session.GroupName}",
                                    ReferenceId = session.SessionId
                                }, ct);
                            }

                            // 3. สร้างประวัติ Transaction ฝั่งผู้เล่น
                            await _context.WalletTransactions.AddAsync(new WalletTransaction
                            {
                                Wallet = wallet,
                                Amount = refundAmount,
                                TransactionType = 1, // IN (Refund)
                                Description = $"คืนเงิน (ก๊วนหมดเวลาโดยผู้จัดไม่ได้เริ่มจัด): {session.GroupName}",
                                ReferenceId = session.SessionId,
                            }, ct);

                            // 4. เปลี่ยนสถานะบิลเป็น Cancelled
                            bill.Status = 3;
                        }
                    }

                    // --- แจ้งเตือนผู้เล่นทุกคนในก๊วน ---
                    var participantUserIds = session.SessionParticipants
                        .Where(p => p.Status != 3)
                        .Select(p => p.UserId)
                        .ToList();

                    foreach (var userId in participantUserIds)
                    {
                        await _notificationService.SendNotificationAsync(
                            userId,
                            "ก๊วนถูกยกเลิกอัตโนมัติ",
                            $"ก๊วน '{session.GroupName}' ถูกยกเลิกเนื่องจากผู้จัดไม่ได้เริ่มจัดก๊วน เงินค่าสนามได้ถูกคืนเข้ากระเป๋าของคุณแล้ว",
                            "SESSION_AUTO_CANCELLED",
                            session.SessionId
                        );
                    }

                    // --- แจ้งเตือนผู้จัดด้วย ---
                    await _notificationService.SendNotificationAsync(
                        session.CreatedByUserId,
                        "ก๊วนถูกยกเลิกอัตโนมัติ",
                        $"ก๊วน '{session.GroupName}' ถูกยกเลิกโดยระบบเนื่องจากเลยเวลาจัดแล้วแต่ไม่ได้กดเริ่ม",
                        "SESSION_AUTO_CANCELLED",
                        session.SessionId
                    );

                    cancelledCount++;
                }
                catch (Exception)
                {
                    // Log error but continue processing other sessions
                    continue;
                }
            }

            if (cancelledCount > 0)
            {
                await _context.SaveChangesAsync(ct);
            }

            return cancelledCount;
        }
    }
}