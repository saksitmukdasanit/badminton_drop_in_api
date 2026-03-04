using System.Globalization;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Hubs;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Service.Mobile.Game
{
    public class GameSessionService : IGameSessionService
    {
        private readonly BadmintonDbContext _context;
        private readonly IHubContext<ManagementGameHub> _hubContext;
        private readonly IMatchManagementService _matchManagementService;

        public GameSessionService(BadmintonDbContext context, IHubContext<ManagementGameHub> hubContext, IMatchManagementService matchManagementService)
        {
            _context = context;
            _hubContext = hubContext;
            _matchManagementService = matchManagementService;
        }

        public async Task<ManageGameSessionDto> CreateSessionAsync(int organizerUserId, SaveGameSessionDto dto)
        {
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

                    // คืนค่าหลังจาก Commit สำเร็จ
                    return (await GetSessionForManageViewAsync(newSession.SessionId, organizerUserId))!;
                }
                catch (Exception ex)
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
            session.Participants.AddRange(registeredParticipants.Select(p => CreateParticipantDto(p, memberGameCounts.ContainsKey(p.UserId) ? memberGameCounts[p.UserId] : 0)));
            session.Participants.AddRange(walkinGuests.Select(g => CreateParticipantDto(g, guestGameCounts.ContainsKey(g.WalkinId) ? guestGameCounts[g.WalkinId] : 0)));

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
                    GroupName = s.GroupName,
                    Status = s.Status ?? 1,
                    VenueData = new VenueDataDto(
                        s.Venue.GooglePlaceId,
                        s.Venue.VenueName,
                        s.Venue.Address,
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

            session.Participants.AddRange(registeredParticipants.Select(p => CreateParticipantDto(p, memberGameCounts.ContainsKey(p.UserId) ? memberGameCounts[p.UserId] : 0)));
            session.Participants.AddRange(walkinGuests.Select(g => CreateParticipantDto(g, guestGameCounts.ContainsKey(g.WalkinId) ? guestGameCounts[g.WalkinId] : 0)));

            session.Participants = session.Participants.OrderBy(p => p.Status).ThenBy(p => p.ParticipantId).ToList();
            session.CurrentParticipants = session.Participants.Count(p => p.Status == 1); // คำนวณจำนวนผู้เล่น

            return session;
        }

        private static ParticipantDto CreateParticipantDto(SessionParticipant p, int gamesPlayed = 0)
        {
            return new ParticipantDto
            {
                ParticipantId = p.ParticipantId,
                ParticipantType = "Member",
                UserId = p.UserId,
                Nickname = p.User.UserProfile!.Nickname,
                FullName = p.User.UserProfile.FirstName + " " + p.User.UserProfile.LastName,
                GenderName = p.User.UserProfile.Gender == 1 ? "ชาย" :
            p.User.UserProfile.Gender == 2 ? "หญิง" :
            p.User.UserProfile.Gender == 3 ? "อื่นๆ" : null,
                ProfilePhotoUrl = p.User.UserProfile.ProfilePhotoUrl,
                SkillLevelId = p.SkillLevelId,
                SkillLevelName = p.SkillLevel!.LevelName,
                SkillLevelColor = p.SkillLevel.ColorHexCode,
                Status = p.Status ?? 1,
                CheckinTime = p.CheckinTime,
                TotalGamesPlayed = gamesPlayed // NEW
            };
        }

        private static ParticipantDto CreateParticipantDto(SessionWalkinGuest g, int gamesPlayed = 0)
        {
            return new ParticipantDto
            {
                ParticipantId = g.WalkinId,
                ParticipantType = "Guest",
                UserId = null,
                Nickname = g.GuestName,
                FullName = null,
                GenderName = g.Gender == 1 ? "ชาย" :
            g.Gender == 2 ? "หญิง" :
            g.Gender == 3 ? "อื่นๆ" : null,
                ProfilePhotoUrl = null, // Walk-in ไม่มีรูปโปรไฟล์ในระบบ
                SkillLevelId = g.SkillLevelId,
                SkillLevelName = g.SkillLevel!.LevelName,
                SkillLevelColor = g.SkillLevel.ColorHexCode,
                Status = g.Status ?? 1,
                CheckinTime = g.CheckinTime,
                TotalGamesPlayed = gamesPlayed // NEW
            };
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
                    Price = (s.CourtFeePerPerson.HasValue || s.ShuttlecockFeePerPerson.HasValue)
                          ? $"{(s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0):N0} บาท"
                          : "สอบถามผู้จัด",
                    OrganizerName = s.CreatedByUser!.UserProfile!.Nickname ?? "N/A",
                    OrganizerImageUrl = s.CreatedByUser.UserProfile.ProfilePhotoUrl,
                    IsBookmarked = userBookmarks.Contains(s.SessionId),
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1),
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
            var session = await _context.GameSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);
            if (session == null) return false;

            session.Status = 3; // 3 = Cancelled
            session.UpdatedDate = DateTime.UtcNow;

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

        public async Task<PlayerGameSessionViewDto?> GetSessionForPlayerViewAsync(int sessionId, int? currentUserId)
        {
            var session = await _context.GameSessions
                .Where(s => s.SessionId == sessionId)
                .Include(s => s.Venue)
                .Include(s => s.ShuttlecockModel).ThenInclude(m => m.Brand)
                .Include(s => s.GameSessionPhotos)
                .Include(s => s.GameSessionFacilities).ThenInclude(f => f.Facility)
                .Include(s => s.CreatedByUser).ThenInclude(u => u.UserProfile) // << ดึงข้อมูลผู้จัด
                .Select(s => new PlayerGameSessionViewDto
                {
                    SessionId = s.SessionId,
                    GroupName = s.GroupName,
                    Status = s.Status ?? 1,
                    SessionStart = s.SessionDate.ToDateTime(s.StartTime),
                    SessionEnd = s.SessionDate.ToDateTime(s.EndTime),
                    VenueName = s.Venue.VenueName,
                    VenueAddress = s.Venue.Address,
                    Latitude = s.Venue.Latitude,
                    Longitude = s.Venue.Longitude,
                    Organizer = new OrganizerInfoDto
                    {
                        UserId = s.CreatedByUserId,
                        Nickname = s.CreatedByUser.UserProfile.Nickname,
                        ProfilePhotoUrl = s.CreatedByUser.UserProfile.ProfilePhotoUrl
                    },
                    ShuttlecockInfo = s.ShuttlecockModel != null ? $"{s.ShuttlecockModel.Brand.BrandName} - {s.ShuttlecockModel.ModelName}" : null,
                    // ... map ข้อมูลอื่นๆ ...
                    MaxParticipants = s.MaxParticipants,
                    Notes = s.Notes,
                    PhotoUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
                    Facilities = s.GameSessionFacilities.Select(f => new FacilityDto(f.FacilityId, f.Facility.FacilityName, f.Facility.IconUrl)).ToList()
                })
                .FirstOrDefaultAsync();

            if (session == null) return null;

            // ดึงรายชื่อผู้เล่น (เหมือนเดิม)
            var participants = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId && p.Status != 3) // ไม่แสดงคนที่ยกเลิก
                .Select(p => new ParticipantDto
                {
                    ParticipantId = p.ParticipantId,
                    ParticipantType = "Member",
                    UserId = p.UserId,

                    // --- ข้อมูลจาก UserProfile ---
                    // ใช้ ! (Null-Forgiving Operator) เพราะ .Include() ทำให้เรามั่นใจว่า UserProfile จะไม่เป็น null
                    Nickname = p.User!.UserProfile!.Nickname,
                    FullName = p.User.UserProfile.FirstName + " " + p.User.UserProfile.LastName,
                    GenderName = p.User.UserProfile.Gender == 1 ? "ชาย" :
                    p.User.UserProfile.Gender == 2 ? "หญิง" :
                    p.User.UserProfile.Gender == 3 ? "อื่นๆ" : null,
                    ProfilePhotoUrl = p.User.UserProfile.ProfilePhotoUrl,

                    // --- ข้อมูลจาก OrganizerSkillLevel ---
                    // ใช้ ?. (Null-Conditional Operator) เพราะผู้เล่นอาจจะยังไม่ถูกกำหนดระดับมือ (SkillLevelId อาจเป็น null)
                    SkillLevelId = p.SkillLevelId,
                    SkillLevelName = p.SkillLevel!.LevelName,
                    SkillLevelColor = p.SkillLevel.ColorHexCode,

                    // --- ข้อมูลสถานะ ---
                    Status = p.Status ?? 1, // ถ้า Status เป็น null ให้ถือว่าเป็น 1 (เข้าร่วม)
                    CheckinTime = p.CheckinTime
                })
                .ToListAsync();
            session.Participants = participants;
            session.CurrentParticipants = participants.Count(p => p.Status == 1);

            // **สำคัญ:** ตรวจสอบสถานะของผู้ใช้ปัจจุบัน
            session.CurrentUserStatus = "NotJoined";
            if (currentUserId.HasValue)
            {
                var currentUserParticipation = await _context.SessionParticipants
                    .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == currentUserId.Value);

                if (currentUserParticipation != null)
                {
                    session.CurrentUserStatus = currentUserParticipation.Status switch
                    {
                        1 => "Joined",
                        2 => "Waitlisted",
                        _ => "NotJoined"
                    };
                }
            }

            return session;
        }


        public async Task<(JoinSessionResponseDto? Data, string ErrorMessage)> JoinSessionAsync(int sessionId, int userId)
        {
            // 1. ค้นหาก๊วนที่จะเข้าร่วม
            var session = await _context.GameSessions
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests) // เพิ่ม: โหลด Walk-in มาด้วยเพื่อนับจำนวนให้ถูกต้อง
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return (null, "Session not found.");
            if (session.Status != 1) return (null, "This session is no longer open for booking.");

            // 2. ตรวจสอบว่าผู้จัดพยายามจองก๊วนตัวเองหรือไม่ (ถ้าไม่ต้องการให้ทำ)
            if (session.CreatedByUserId == userId)
            {
                return (null, "Organizers cannot join their own session as a participant.");
            }

            // 3. ตรวจสอบว่าเคยจองไปแล้วหรือยัง (เผื่อกรณีกดซ้ำ)
            var existingParticipant = session.SessionParticipants.FirstOrDefault(p => p.UserId == userId);
            if (existingParticipant != null && existingParticipant.Status != 3) // 3 = Cancelled
            {
                return (null, "You are already registered for this session.");
            }

            int newStatus;
            string statusMessage;

            // 4. ตรวจสอบว่าก๊วนเต็มหรือยัง
            var activeParticipants = session.SessionParticipants.Count(p => p.Status == 1) + session.SessionWalkinGuests.Count(g => g.Status == 1);
            var waitlistedParticipants = session.SessionParticipants.Count(p => p.Status == 2) + session.SessionWalkinGuests.Count(g => g.Status == 2);

            // เงื่อนไขใหม่: ต้องไม่เต็ม AND ต้องไม่มีใครรอคิวอยู่ ถึงจะได้เป็นตัวจริง
            if (activeParticipants < session.MaxParticipants && waitlistedParticipants == 0)
            {
                // ยังไม่เต็ม -> เข้าร่วมเป็นตัวจริง
                newStatus = 1;
                statusMessage = "Joined successfully.";
            }
            else
            {
                // ก๊วนเต็ม หรือ มีคนรอคิวอยู่ -> ไปต่อคิวสำรอง
                newStatus = 2;
                statusMessage = "You are on the waitlist.";
            }

            // 5. บันทึกข้อมูล
            SessionParticipant newParticipantEntry;
            if (existingParticipant != null) // กรณีกลับมาจองใหม่หลังจากเคยกด Cancel
            {
                existingParticipant.Status = (byte)newStatus;
                existingParticipant.JoinedDate = DateTime.UtcNow;
                newParticipantEntry = existingParticipant;
            }
            else // กรณีจองครั้งแรก
            {
                newParticipantEntry = new SessionParticipant
                {
                    SessionId = sessionId,
                    UserId = userId,
                    Status = (byte)newStatus,
                    JoinedDate = DateTime.UtcNow
                };
                await _context.SessionParticipants.AddAsync(newParticipantEntry);
            }

            await _context.SaveChangesAsync();

            var responseDto = new JoinSessionResponseDto
            {
                ParticipantId = newParticipantEntry.ParticipantId,
                Status = newStatus,
                StatusMessage = statusMessage
            };

            return (responseDto, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> CancelBookingAsync(int sessionId, int userId)
        {
            var participant = await _context.SessionParticipants
                .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);

            if (participant == null || participant.Status == 3) // ถ้าไม่เจอ หรือยกเลิกไปแล้ว
            {
                return (false, "Booking not found.");
            }

            // 1. ตั้งค่าสถานะเป็น "ยกเลิก"
            participant.Status = 3;

            // --- REMOVED: Auto-promote logic for user cancellation ---
            // ปิดการเลื่อนสถานะอัตโนมัติเมื่อผู้เล่นกดยกเลิกเอง
            // เพื่อให้ผู้จัดเป็นคนจัดการคิวสำรองเองทั้งหมด

            await _context.SaveChangesAsync();
            return (true, "Your booking has been cancelled.");
        }

        public async Task<(ParticipantDto? Data, string ErrorMessage)> AddGuestAsync(int sessionId, int organizerUserId, AddGuestDto dto)
        {
            var session = await _context.GameSessions
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null)
            {
                return (null, "Session not found or you do not have permission.");
            }

            byte newStatus;
            var currentParticipants = session.SessionParticipants.Count(p => p.Status == 1) + session.SessionWalkinGuests.Count(g => g.Status == 1);
            var waitlistedParticipants = session.SessionParticipants.Count(p => p.Status == 2) + session.SessionWalkinGuests.Count(g => g.Status == 2);

            // เงื่อนไขใหม่: ถ้าเต็ม หรือ มีคนรอคิวอยู่ ให้ไปเป็นสำรอง
            if (currentParticipants >= session.MaxParticipants || waitlistedParticipants > 0)
            {
                newStatus = 2; // 2 = Waitlisted
            }
            else
            {
                newStatus = 1; // 1 = Joined
            }

            var newGuest = new SessionWalkinGuest
            {
                SessionId = sessionId,
                GuestName = dto.GuestName,
                PhoneNumber = dto.PhoneNumber,
                Gender = (short)dto.Gender,
                SkillLevelId = dto.SkillLevelId,
                Status = newStatus,
                CreatedBy = organizerUserId,
                CreatedDate = DateTime.UtcNow,
                CheckinTime = DateTime.UtcNow,
            };

            await _context.SessionWalkinGuests.AddAsync(newGuest);
            await _context.SaveChangesAsync();

            // ดึงข้อมูล SkillLevel เพื่อสร้าง ParticipantDto ที่สมบูรณ์
            var skillLevel = dto.SkillLevelId.HasValue
                ? await _context.OrganizerSkillLevels.FindAsync(dto.SkillLevelId.Value)
                : null;

            newGuest.SkillLevel = skillLevel; // Attach the loaded skill level

            return (CreateParticipantDto(newGuest), string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> UpdateParticipantSkillLevelAsync(int sessionId, string participantType, int participantId, int? newSkillLevelId, int organizerUserId)
        {
            var session = await _context.GameSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null)
            {
                return (false, "Session not found or you do not have permission.");
            }

            if (participantType.Equals("Member", StringComparison.OrdinalIgnoreCase))
            {
                var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.ParticipantId == participantId && p.SessionId == sessionId);
                if (participant == null)
                {
                    return (false, "Participant not found in this session.");
                }
                participant.SkillLevelId = newSkillLevelId;
            }
            else if (participantType.Equals("Guest", StringComparison.OrdinalIgnoreCase))
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

            var sessions = await _context.GameSessions
        .Where(s => s.CreatedByUserId == organizerUserId && s.SessionDate >= today && (s.Status == 1 || s.Status == 2))
        .Include(s => s.Venue)
        .Include(s => s.GameSessionPhotos)
        .Include(s => s.CreatedByUser.UserProfile)
        .Include(s => s.SessionParticipants) // Include ไว้เพื่อนับจำนวน
        .Include(s => s.SessionWalkinGuests) // Include ไว้เพื่อนับจำนวน
        .Include(s => s.GameType)
        .Include(s => s.ShuttlecockModel).ThenInclude(m => m!.Brand)
        .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
        .Select(s => new UpcomingSessionCardDto
        {
            // --- Map ข้อมูลทั้งหมดเหมือนเดิม ---
            SessionId = s.SessionId,
            GroupName = s.GroupName,
            ImageUrl = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).FirstOrDefault(),
            DayOfWeek = s.SessionDate.ToDateTime(TimeOnly.MinValue).ToString("dddd", thaiCulture),
            SessionDate = s.SessionDate.ToString("dd/MM/yyyy", thaiCulture),
            StartTime = s.StartTime.ToString("HH:mm"),
            EndTime = s.EndTime.ToString("HH:mm"),
            SessionStart = s.SessionDate.ToDateTime(s.StartTime),
            CourtName = s.Venue.VenueName,
            Location = s.Venue.Address,
            Price = (s.CourtFeePerPerson.HasValue || s.ShuttlecockFeePerPerson.HasValue)
                  ? $"{(s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0):N0} บาท"
                  : "สอบถามผู้จัด",
            CourtFeePerPerson = s.CourtFeePerPerson.ToString(),
            ShuttlecockFeePerPerson = s.ShuttlecockFeePerPerson.ToString(),
            OrganizerName = s.CreatedByUser!.UserProfile!.Nickname ?? "N/A",
            OrganizerImageUrl = s.CreatedByUser.UserProfile.ProfilePhotoUrl,
            IsBookmarked = false,
            MaxParticipants = s.MaxParticipants,
            CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1) +
                                  s.SessionWalkinGuests.Count(g => g.Status == 1),
            GameTypeName = s.GameType!.TypeName,
            ShuttlecockBrandName = s.ShuttlecockModel!.Brand!.BrandName,
            ShuttlecockModelName = s.ShuttlecockModel.ModelName,
            CourtImageUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList(),
            Status = s.Status,
            CourtNumbers = s.CourtNumbers,
            Notes = s.Notes,

            Facilities = new List<FacilityDto>(),
            Participants = new List<ParticipantDto>()
        })
        .ToListAsync(); // <--- จบ Query ที่ 1 (ได้ข้อมูลก๊วนทั้งหมดมาแล้ว)

            if (!sessions.Any())
            {
                return sessions; // ถ้าไม่เจอก๊วนเลย ก็คืนค่าลิสต์ว่างๆ กลับไป
            }
            foreach (var session in sessions)
            {
                // Query ย่อยที่ 1: ดึงผู้เล่นที่เป็นสมาชิก
                var members = await _context.SessionParticipants
                    .Where(p => p.SessionId == session.SessionId && p.Status != 3)
                    .Include(p => p.User.UserProfile)
                    .Include(p => p.SkillLevel)
                    .Select(p => new ParticipantDto
                    {
                        ParticipantId = p.ParticipantId,
                        ParticipantType = "Member",
                        UserId = p.UserId,
                        Nickname = p.User!.UserProfile!.Nickname,
                        FullName = p.User.UserProfile.FirstName + " " + p.User.UserProfile.LastName,
                        GenderName = p.User.UserProfile.Gender == 1 ? "ชาย" :
                    p.User.UserProfile.Gender == 2 ? "หญิง" :
                    p.User.UserProfile.Gender == 3 ? "อื่นๆ" : null,
                        ProfilePhotoUrl = p.User.UserProfile.ProfilePhotoUrl,
                        SkillLevelId = p.SkillLevelId,
                        SkillLevelName = p.SkillLevel!.LevelName,
                        SkillLevelColor = p.SkillLevel.ColorHexCode,
                        Status = p.Status ?? 1,
                        CheckinTime = p.CheckinTime
                    }).ToListAsync(); // <--- ทำงานใน C#

                // Query ย่อยที่ 2: ดึงผู้เล่นที่เป็น Walk-in
                var guests = await _context.SessionWalkinGuests
                    .Where(g => g.SessionId == session.SessionId && g.Status != 3)
                    .Include(g => g.SkillLevel)
                    .Select(g => new ParticipantDto
                    {
                        ParticipantId = g.WalkinId,
                        ParticipantType = "Guest",
                        UserId = null,
                        Nickname = g.GuestName,
                        FullName = null,
                        GenderName = g.Gender == 1 ? "ชาย" :
                    g.Gender == 2 ? "หญิง" :
                    g.Gender == 3 ? "อื่นๆ" : null,
                        ProfilePhotoUrl = null,
                        SkillLevelId = g.SkillLevelId,
                        SkillLevelName = g.SkillLevel!.LevelName,
                        SkillLevelColor = g.SkillLevel.ColorHexCode,
                        Status = g.Status ?? 1,
                        CheckinTime = g.CheckinTime
                    }).ToListAsync(); // <--- ทำงานใน C#

                session.Facilities = await _context.GameSessionFacilities
                        .Where(f => f.SessionId == session.SessionId)
                        .Include(f => f.Facility)
                        .Select(f => new FacilityDto(f.FacilityId, f.Facility.FacilityName, f.Facility.IconUrl))
                        .ToListAsync();

                // 3. รวมสองลิสต์นี้เข้าด้วยกัน (ตอนนี้ทำใน C# แล้ว EF ไม่ต้องแปล)
                session.Participants = members.Concat(guests)
                                            .OrderBy(p => p.Status)
                                            .ThenBy(p => p.ParticipantId)
                                            .ToList();

            }

            return sessions;
        }

        public async Task<IEnumerable<OrganizerGameSessionDto>> GetMyPastSessionsAsync(int organizerUserId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            var sessions = await _context.GameSessions
               .Where(s => s.CreatedByUserId == organizerUserId && (s.SessionDate < today || s.Status != 1)) // << กรองอดีต หรือก๊วนที่จบ/ยกเลิกแล้ว (ไม่ Active)
               .Include(s => s.Venue)
               .Include(s => s.SessionParticipants)
               .Include(s => s.SessionWalkinGuests)
               .Include(s => s.ParticipantBills) // ดึงบิลเพื่อคำนวณเงิน
               .OrderByDescending(s => s.SessionDate) // เรียงจากล่าสุดไปเก่าสุด
               .ThenByDescending(s => s.StartTime)
               .ToListAsync();

            var result = new List<OrganizerGameSessionDto>();

            foreach (var s in sessions)
            {
                // คำนวณรายได้
                // 1. รายได้ที่เก็บได้จริง (Paid) = ผลรวมของบิลทั้งหมด
                decimal paidAmount = s.ParticipantBills.Sum(b => b.TotalAmount);

                // 2. รายได้ที่ควรจะได้ (Expected) = จำนวนผู้เล่น * (ค่าคอร์ท + ค่าลูก)
                // นับเฉพาะคนที่ Status = 1 (Joined)
                int memberCount = s.SessionParticipants.Count(p => p.Status == 1);
                int guestCount = s.SessionWalkinGuests.Count(g => g.Status == 1);
                int totalPlayers = memberCount + guestCount;

                decimal feePerPerson = (s.CourtFeePerPerson ?? 0) + (s.ShuttlecockFeePerPerson ?? 0);
                decimal expectedIncome = totalPlayers * feePerPerson;

                // 3. ค้างจ่าย (Unpaid)
                // กรณีที่เก็บเงินได้น้อยกว่าที่ควรจะได้ (หรืออาจจะใช้ Logic ว่าใครยังไม่มีบิลก็ได้ แต่วิธีนี้ง่ายกว่าสำหรับภาพรวม)
                decimal unpaidAmount = expectedIncome - paidAmount;
                if (unpaidAmount < 0) unpaidAmount = 0; // ป้องกันติดลบกรณีเก็บเกินหรือทิป

                result.Add(new OrganizerGameSessionDto
                {
                    GameSessionId = s.SessionId,
                    Date = s.SessionDate.ToDateTime(s.StartTime),
                    GroupName = s.GroupName,
                    TotalIncome = expectedIncome, // รายได้รวมที่ควรจะได้
                    PaidAmount = paidAmount,      // จ่ายแล้ว
                    UnpaidAmount = unpaidAmount,  // ค้างจ่าย
                    Status = s.Status == 3 ? "Cancelled" : (s.Status == 2 ? "Started" : "Open"),
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
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User.UserProfile)
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

        public async Task<GameSessionFinancialsDto?> GetSessionFinancialsAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions
                .Include(s => s.SessionParticipants).ThenInclude(p => p.User.UserProfile)
                .Include(s => s.SessionWalkinGuests)
                .Include(s => s.ParticipantBills)
                .Include(s => s.Matches).ThenInclude(m => m.MatchPlayers)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null) return null;

            // 1. Participants (Active only)
            var activeMembers = session.SessionParticipants.Where(p => p.Status == 1).ToList();
            var activeGuests = session.SessionWalkinGuests.Where(g => g.Status == 1).ToList();
            int currentParticipants = activeMembers.Count + activeGuests.Count;

            // 2. Fees & Costs
            decimal courtFeePerPerson = session.CourtFeePerPerson ?? 0;
            decimal shuttleFeePerPerson = session.ShuttlecockFeePerPerson ?? 0;
            decimal totalCourtCost = session.TotalCourtCost ?? 0; // ต้นทุนที่ผู้จัดจ่าย
            decimal shuttleCostPerUnit = session.ShuttlecockCostPerUnit ?? 0;

            // Helper to count games
            int CountGames(int? userId, int? walkinId)
            {
                return session.Matches.Count(m => m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == userId && mp.WalkinId == walkinId));
            }
            
            // ตัวแปรสำหรับสะสมยอดรวม (Aggregation)
            decimal aggTotalCourtIncome = 0;
            decimal aggTotalShuttleFee = 0;
            decimal aggTotalIncome = 0;
            decimal aggPaidAmount = 0;
            decimal aggUnpaidAmount = 0;

            // --- NEW: ตัวแปรสำหรับสรุปยอดละเอียด ---
            int countPaidCourt = 0;
            int countUnpaidCourt = 0;
            decimal sumPaidCourt = 0;
            decimal sumUnpaidCourt = 0;
            decimal sumPaidShuttle = 0;
            decimal sumUnpaidShuttle = 0;
            decimal sumAdditions = 0;
            decimal sumSubtractions = 0;

            var participantDtos = new List<ParticipantFinancialDto>();

            // Helper ใหม่: คำนวณยอดเงินรายคนและแยกส่วนประกอบ
            (decimal paid, decimal total, decimal courtPart, decimal shuttlePart) CalculateParticipantFinancials(int? userId, int? walkinId, int gamesPlayed)
            {
                // 1. คำนวณค่าใช้จ่ายมาตรฐาน
                decimal cPart = courtFeePerPerson;
                decimal sPart = 0;
                if (session.CostingMethod == 2) // Buffet
                {
                    sPart = shuttleFeePerPerson;
                }
                else 
                {
                    sPart = shuttleFeePerPerson * gamesPlayed;
                }

                // 2. ตรวจสอบบิล
                var bills = session.ParticipantBills.Where(b => b.UserId == userId && b.WalkinId == walkinId && b.Status != 3).ToList();
                decimal paidVal = bills.Where(b => b.Status == 2).Sum(b => b.TotalAmount);
                
                decimal totalVal = 0;
                if (paidVal > 0)
                {
                    // ถ้าจ่ายแล้ว ให้ใช้ยอดจากบิล (เพราะอาจมีการแก้ราคา)
                    totalVal = bills.Sum(b => b.TotalAmount);
                }
                else
                {
                    // ถ้ายังไม่จ่าย ให้ใช้ยอดคำนวณมาตรฐาน (+10 ค่าบริการ)
                    totalVal = cPart + sPart + 10; 
                }

                return (paidVal, totalVal, cPart, sPart);
            }

            // Helper สำหรับคำนวณสัดส่วนการจ่าย (Ratio Logic ย้ายมาจาก Frontend)
            void CalculateBreakdown(decimal totalCost, decimal paidAmount, decimal courtFee, decimal shuttleFee)
            {
                decimal ratio = totalCost > 0 ? paidAmount / totalCost : 0;
                if (ratio > 1) ratio = 1;

                // Court
                decimal cPaid = courtFee * ratio;
                decimal cUnpaid = courtFee - cPaid;
                sumPaidCourt += cPaid;
                sumUnpaidCourt += cUnpaid;
                if (cUnpaid <= 1) countPaidCourt++; else countUnpaidCourt++;

                // Shuttle
                decimal sPaid = shuttleFee * ratio;
                decimal sUnpaid = shuttleFee - sPaid;
                sumPaidShuttle += sPaid;
                sumUnpaidShuttle += sUnpaid;

                // Additions/Subtractions (ส่วนต่างจากค่ามาตรฐาน)
                decimal standardTotal = courtFee + shuttleFee + 10; // +10 Service Fee
                decimal diff = totalCost - standardTotal;
                // หมายเหตุ: Logic นี้เป็นการประมาณการคร่าวๆ จากยอดรวม
                if (diff > 0.1m) sumAdditions += diff;
                else if (diff < -0.1m) sumSubtractions += diff;
            }

            foreach (var m in activeMembers)
            {
                int games = CountGames(m.UserId, null);
                var (paid, total, cPart, sPart) = CalculateParticipantFinancials(m.UserId, null, games);
                
                // สะสมยอดรวม
                aggTotalCourtIncome += cPart;
                aggTotalShuttleFee += sPart;
                aggTotalIncome += total;
                aggPaidAmount += paid;
                aggUnpaidAmount += (total - paid > 0 ? total - paid : 0);

                CalculateBreakdown(total, paid, cPart, sPart);

                participantDtos.Add(new ParticipantFinancialDto
                {
                    ParticipantId = m.ParticipantId,
                    ParticipantType = "Member",
                    Nickname = m.User?.UserProfile?.Nickname ?? "N/A",
                    Name = $"{m.User?.UserProfile?.FirstName} {m.User?.UserProfile?.LastName}",
                    GamesPlayed = games,
                    TotalCost = total,
                    PaidAmount = paid,
                    UnpaidAmount = total - paid > 0 ? total - paid : 0,
                    CourtFee = cPart,   // ส่งค่าสนามที่คำนวณจาก API
                    ShuttleFee = sPart  // ส่งค่าลูกแบดที่คำนวณจาก API
                });
            }

            foreach (var g in activeGuests)
            {
                int games = CountGames(null, g.WalkinId);
                var (paid, total, cPart, sPart) = CalculateParticipantFinancials(null, g.WalkinId, games);

                // สะสมยอดรวม
                aggTotalCourtIncome += cPart;
                aggTotalShuttleFee += sPart;
                aggTotalIncome += total;
                aggPaidAmount += paid;
                aggUnpaidAmount += (total - paid > 0 ? total - paid : 0);

                CalculateBreakdown(total, paid, cPart, sPart);

                participantDtos.Add(new ParticipantFinancialDto
                {
                    ParticipantId = g.WalkinId,
                    ParticipantType = "Guest",
                    Nickname = g.GuestName,
                    Name = g.GuestName,
                    GamesPlayed = games,
                    TotalCost = total,
                    PaidAmount = paid,
                    UnpaidAmount = total - paid > 0 ? total - paid : 0,
                    CourtFee = cPart,   // ส่งค่าสนามที่คำนวณจาก API
                    ShuttleFee = sPart  // ส่งค่าลูกแบดที่คำนวณจาก API
                });
            }

            // คำนวณต้นทุนรวม (ค่าสนาม + ค่าลูกแบดที่ใช้จริง)
            int totalShuttlecocksUsed = session.Matches.Count(m => m.Status == 2);
            decimal totalShuttleCost = totalShuttlecocksUsed * shuttleCostPerUnit;

            // คำนวณยอดเงินสดและเงินโอน
            var payments = await _context.Payments
                .Where(p => p.Bill.SessionId == sessionId)
                .ToListAsync();
            decimal totalCash = payments.Where(p => p.PaymentMethod == 1).Sum(p => p.Amount);
            decimal totalTransfer = payments.Where(p => p.PaymentMethod == 2).Sum(p => p.Amount);

            return new GameSessionFinancialsDto
            {
                SessionId = session.SessionId,
                GroupName = session.GroupName,
                CurrentParticipants = currentParticipants,
                CourtFeePerPerson = courtFeePerPerson,
                ShuttlecockFeePerPerson = shuttleFeePerPerson,
                ShuttlecockCostPerUnit = shuttleCostPerUnit, // ส่งราคาทุน
                TotalCourtCost = totalCourtCost,
                TotalCourtIncome = aggTotalCourtIncome, // ยอดรวมจากทุกคน
                TotalShuttlecockFee = aggTotalShuttleFee, // ยอดรวมจากทุกคน
                TotalShuttlecockCost = totalShuttleCost, // ส่งต้นทุนรวม
                TotalIncome = aggTotalIncome, // ยอดรวมจากทุกคน (รวมค่าบริการ)
                TotalExpense = totalCourtCost + totalShuttleCost, // ต้นทุนสนาม + ต้นทุนลูก
                PaidAmount = aggPaidAmount,
                TotalCashAmount = totalCash,
                TotalTransferAmount = totalTransfer,
                UnpaidAmount = aggUnpaidAmount,
                TotalShuttlecocks = totalShuttlecocksUsed,
                Participants = participantDtos,
                // --- NEW: ส่งค่าสรุปละเอียดกลับไป ---
                PaidCourtCount = countPaidCourt,
                UnpaidCourtCount = countUnpaidCourt,
                PaidCourtAmount = sumPaidCourt,
                UnpaidCourtAmount = sumUnpaidCourt,
                PaidShuttleAmount = sumPaidShuttle,
                UnpaidShuttleAmount = sumUnpaidShuttle,
                TotalAdditions = sumAdditions,
                TotalSubtractions = sumSubtractions
            };
        }

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
            return true;
        }

        public async Task<(bool Success, string ErrorMessage)> RemoveParticipantAsync(int sessionId, string participantType, int participantId, int organizerUserId)
        {
            var session = await _context.GameSessions
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null) return (false, "Session not found or permission denied.");

            bool wasActive = false;

            if (participantType.Equals("member", StringComparison.OrdinalIgnoreCase))
            {
                var p = session.SessionParticipants.FirstOrDefault(x => x.ParticipantId == participantId);
                if (p == null) return (false, "Participant not found.");
                
                if (p.Status == 1) wasActive = true;
                p.Status = 3; // 3 = Removed/Cancelled
                p.CheckoutTime = DateTime.UtcNow; // Mark timestamp
            }
            else if (participantType.Equals("guest", StringComparison.OrdinalIgnoreCase))
            {
                var g = session.SessionWalkinGuests.FirstOrDefault(x => x.WalkinId == participantId);
                if (g == null) return (false, "Guest not found.");

                if (g.Status == 1) wasActive = true;
                g.Status = 3; // 3 = Removed/Cancelled
                g.CheckoutTime = DateTime.UtcNow;
            }
            else
            {
                return (false, "Invalid participant type.");
            }

            // --- REMOVED: Auto-promote logic ---
            // ไม่ต้องเลื่อนตัวสำรองขึ้นมาอัตโนมัติ ให้ผู้จัดเลือกเอง

            await _context.SaveChangesAsync();
            return (true, "Participant removed successfully.");
        }

        public async Task<(bool Success, string ErrorMessage)> PromoteWaitlistedParticipantAsync(int sessionId, string participantType, int participantId, int organizerUserId)
        {
            var session = await _context.GameSessions
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null) return (false, "Session not found or permission denied.");

            // ตรวจสอบว่าก๊วนเต็มหรือยัง
            int currentCount = session.SessionParticipants.Count(p => p.Status == 1) + 
                               session.SessionWalkinGuests.Count(g => g.Status == 1);
            
            if (currentCount >= session.MaxParticipants)
            {
                return (false, "Session is full. Cannot promote participant.");
            }

            if (participantType.Equals("member", StringComparison.OrdinalIgnoreCase))
            {
                var p = session.SessionParticipants.FirstOrDefault(x => x.ParticipantId == participantId);
                if (p == null) return (false, "Participant not found.");
                if (p.Status != 2) return (false, "Participant is not in waitlist.");
                p.Status = 1; // Promote to Joined
            }
            else if (participantType.Equals("guest", StringComparison.OrdinalIgnoreCase))
            {
                var g = session.SessionWalkinGuests.FirstOrDefault(x => x.WalkinId == participantId);
                if (g == null) return (false, "Guest not found.");
                if (g.Status != 2) return (false, "Guest is not in waitlist.");
                g.Status = 1; // Promote to Joined
            }
            else
            {
                return (false, "Invalid participant type.");
            }

            await _context.SaveChangesAsync();
            return (true, "Participant promoted successfully.");
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

            // 1. หาผู้เล่นที่ "ไม่ว่าง" (เล่นอยู่ หรือ รอคิวในสนาม)
            // Status 4 = Staged, 1 = Playing (แก้ไขจาก 0 เป็น 4)
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

            // 2. รวบรวมผู้เล่นที่ "ว่าง" และ "พร้อม" (Status = 1 Joined)
            var availablePlayers = new List<dynamic>(); // ใช้ dynamic หรือ class ย่อยเพื่อคำนวณ

            // Helper เช็ค Excluded (Paused/Ended)
            // FIX: เปรียบเทียบแบบ Case-Insensitive เพื่อความชัวร์
            bool IsExcluded(string type, int id) 
            {
                var targetId = $"{type}_{id}";
                return dto.ExcludedPlayerIds.Any(ex => string.Equals(ex, targetId, StringComparison.OrdinalIgnoreCase));
            }

            // สมาชิก
            foreach (var p in session.SessionParticipants.Where(p => p.Status == 1))
            {
                // กรองเฉพาะคนที่ Check-in แล้วเท่านั้น
                if (p.CheckinTime == null) continue;

                if (busyUserIds.Contains(p.UserId) || IsExcluded("Member", p.ParticipantId)) continue;
                
                // คำนวณ Games Played และ Waiting Time
                var playedMatches = session.Matches.Where(m => m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == p.UserId)).OrderByDescending(m => m.EndTime).ToList();
                int gamesPlayed = playedMatches.Count;
                DateTime waitingSince = playedMatches.FirstOrDefault()?.EndTime ?? p.CheckinTime ?? DateTime.UtcNow;

                availablePlayers.Add(new { 
                    Id = p.ParticipantId, Type = "Member", UserId = (int?)p.UserId, WalkinId = (int?)null,
                    Skill = p.SkillLevelId ?? 0, Games = gamesPlayed, Wait = waitingSince 
                });
            }

            // Walk-in
            foreach (var g in session.SessionWalkinGuests.Where(g => g.Status == 1))
            {
                // กรองเฉพาะคนที่ Check-in แล้วเท่านั้น (เผื่อกรณีข้อมูลเก่า หรือมีการแก้ Logic ในอนาคต)
                if (g.CheckinTime == null) continue;

                if (busyWalkinIds.Contains(g.WalkinId) || IsExcluded("Guest", g.WalkinId)) continue;

                var playedMatches = session.Matches.Where(m => m.Status == 2 && m.MatchPlayers.Any(mp => mp.WalkinId == g.WalkinId)).OrderByDescending(m => m.EndTime).ToList();
                int gamesPlayed = playedMatches.Count;
                DateTime waitingSince = playedMatches.FirstOrDefault()?.EndTime ?? g.CheckinTime ?? DateTime.UtcNow;

                availablePlayers.Add(new { 
                    Id = g.WalkinId, Type = "Guest", UserId = (int?)null, WalkinId = (int?)g.WalkinId,
                    Skill = g.SkillLevelId ?? 0, Games = gamesPlayed, Wait = waitingSince 
                });
            }

            // 3. เรียงลำดับ (Games น้อยสุด -> รอนานสุด)
            if (availablePlayers.Count < 4) return (false, "Not enough players available (need 4).");

            var sortedPlayers = availablePlayers
                .OrderBy(p => p.Games)
                .ThenBy(p => p.Wait)
                .Take(4)
                .ToList();

            // 4. จัดทีม (Algorithm)
            List<dynamic> teamA = new();
            List<dynamic> teamB = new();

            if (dto.IsMixedMode)
            {
                // สูตร Mixed: เรียง Skill น้อย->มาก แล้วจับคู่ (อ่อนสุด+เก่งสุด) vs (กลาง+กลาง)
                var pSorted = sortedPlayers.OrderBy(p => (int)p.Skill).ToList();
                teamA.Add(pSorted[0]); teamA.Add(pSorted[3]);
                teamB.Add(pSorted[1]); teamB.Add(pSorted[2]);
            }
            else
            {
                // สูตร Skill: เรียงตาม Skill แล้วแบ่งครึ่ง (อ่อน+อ่อน) vs (เก่ง+เก่ง) หรือตามความเหมาะสม
                // ในที่นี้ใช้สูตรเดียวกับ Mixed ไปก่อนเพื่อความง่าย หรือปรับตามต้องการ
                var pSorted = sortedPlayers.OrderBy(p => (int)p.Skill).ToList();
                teamA.Add(pSorted[0]); teamA.Add(pSorted[3]);
                teamB.Add(pSorted[1]); teamB.Add(pSorted[2]);
            }

            // 5. หาสนามว่าง
            // ดึงหมายเลขสนามทั้งหมดที่มี
            var allCourts = !string.IsNullOrEmpty(session.CourtNumbers) 
                ? session.CourtNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
                : new List<string>();
            
            // FIX: เพิ่ม Fallback กรณี CourtNumbers เป็นค่าว่าง ให้สร้างเลขสนามตามจำนวน NumberOfCourts
            if (!allCourts.Any())
            {
                allCourts = Enumerable.Range(1, session.NumberOfCourts ?? 1).Select(i => i.ToString()).ToList();
            }

            // ดึงสนามที่ใช้อยู่
            // FIX: นับเฉพาะสนามที่มีคนเล่น หรือมีคนรอ (ไม่นับ Ghost Match ที่ไม่มีคน)
            var usedCourts = activeMatches
                .Where(m => m.Status == 1 || m.MatchPlayers.Any()) 
                .Select(m => m.CourtNumber)
                .ToHashSet();
            
            string targetCourt = null;
            foreach (var court in allCourts)
            {
                if (!usedCourts.Contains(court))
                {
                    targetCourt = court;
                    break;
                }
            }

            // ถ้าสนามเต็ม ให้ลงทีมสำรอง (ใช้รหัสติดลบ -1, -2)
            if (targetCourt == null)
            {
                int reserveIndex = 1;
                while (usedCourts.Contains($"-{reserveIndex}"))
                {
                    reserveIndex++;
                }
                targetCourt = $"-{reserveIndex}";
            }

            // 6. สร้าง Match (Staged) หรือใช้ Ghost Match เดิมที่ว่างอยู่
            Match newMatch;
            var ghostMatch = activeMatches.FirstOrDefault(m => m.CourtNumber == targetCourt && m.Status == 4 && !m.MatchPlayers.Any());

            if (ghostMatch != null)
            {
                // Reuse แมตช์เดิม
                newMatch = ghostMatch;
                newMatch.CreatedDate = DateTime.UtcNow; // อัปเดตเวลา
                newMatch.MatchPlayers.Clear(); // เคลียร์ผู้เล่น (เผื่อมีขยะ)
            }
            else
            {
                // สร้างใหม่
                newMatch = new Match
                {
                    SessionId = sessionId,
                    CourtNumber = targetCourt,
                    Status = 4, // 4 = Staged
                    CreatedDate = DateTime.UtcNow,
                    ShuttlecocksUsed = 0,
                    MatchPlayers = new List<MatchPlayer>()
                };
                _context.Matches.Add(newMatch);
            }

            // Add Players
            foreach (var p in teamA)
            {
                newMatch.MatchPlayers.Add(new MatchPlayer
                {
                    UserId = p.UserId, // ค่านี้ถูกต้องจาก availablePlayers แล้ว
                    WalkinId = p.WalkinId, // ค่านี้ถูกต้องจาก availablePlayers แล้ว
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

            // ดึง Staged Matches ทั้งหมด (Status 4)
            var stagedMatches = session.Matches.Where(m => m.Status == 4).ToList();

            // FIX: ต้องแปลง ParticipantId เป็น UserId สำหรับ Member ก่อนค้นหา
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
            Match? match1 = null;
            Match? match2 = null;

            // ค้นหาผู้เล่นทั้งสองคนใน Staged Matches
            foreach (var match in stagedMatches)
            {
                var p1 = match.MatchPlayers.FirstOrDefault(p => 
                    (string.Equals(dto.Player1.Type, "Member", StringComparison.OrdinalIgnoreCase) && p.UserId == p1UserId) || 
                    (string.Equals(dto.Player1.Type, "Guest", StringComparison.OrdinalIgnoreCase) && p.WalkinId == dto.Player1.Id));
                
                if (p1 != null) { mp1 = p1; match1 = match; }

                var p2 = match.MatchPlayers.FirstOrDefault(p => 
                    (string.Equals(dto.Player2.Type, "Member", StringComparison.OrdinalIgnoreCase) && p.UserId == p2UserId) || 
                    (string.Equals(dto.Player2.Type, "Guest", StringComparison.OrdinalIgnoreCase) && p.WalkinId == dto.Player2.Id));
                
                if (p2 != null) { mp2 = p2; match2 = match; }
            }

            if (mp1 == null || mp2 == null) return (false, "One or both players not found in staged matches.");

            // สลับข้อมูล (UserId/WalkinId) ระหว่าง 2 Record
            // หมายเหตุ: เราสลับค่า ID แทนการสลับ Object เพื่อความง่ายในการจัดการ EF Core Tracking
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

            // 1. หา Staged Match ของสนามเป้าหมาย (ถ้ามี) หรือเตรียมสร้างใหม่
            var targetMatch = session.Matches.FirstOrDefault(m => m.Status == 4 && m.CourtNumber == dto.TargetCourtIdentifier);
            
            // ถ้าสนามไม่ว่าง (มีคนยืนอยู่) ให้เคลียร์คนออกก่อน (ตาม Logic เดิมคือแทนที่)
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
                    Status = 4, // FIX: 4 = Staged
                    CreatedDate = DateTime.UtcNow,
                    MatchPlayers = new List<MatchPlayer>()
                };
                _context.Matches.Add(targetMatch);
            }

            // 2. หา Reserve Team ที่เหมาะสม
            Match? reserveMatch = null;
            var reserveMatches = session.Matches
                .Where(m => m.Status == 4 && m.CourtNumber != null && m.CourtNumber.StartsWith("-"))
                .ToList();

            if (dto.IsQueueMode)
            {
                // โหมดคิว: เอาทีมสำรองทีมแรกที่ "พร้อม" (มีคน)
                // เรียงตามลำดับเลขลบ (เช่น -1, -2, -3) -> -1 มาก่อน
                reserveMatch = reserveMatches
                    .Where(m => m.MatchPlayers.Any()) // ต้องมีคน
                    .OrderByDescending(m => int.Parse(m.CourtNumber!)) // -1 > -2
                    .FirstOrDefault();
            }
            else
            {
                // โหมดสนาม: เอาทีมสำรองที่เลขตรงกับสนาม (เช่น สนาม 1 -> สำรอง -1)
                // สมมติ Logic: Court "1" maps to Reserve "-1"
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

            // 3. ย้ายผู้เล่นจาก Reserve -> Target
            foreach (var p in reserveMatch.MatchPlayers)
            {
                targetMatch.MatchPlayers.Add(new MatchPlayer
                {
                    UserId = p.UserId,
                    WalkinId = p.WalkinId,
                    Team = p.Team
                });
            }

            // 4. ลบผู้เล่นออกจาก Reserve (เคลียร์ทีมสำรอง)
            _context.MatchPlayers.RemoveRange(reserveMatch.MatchPlayers);
            
            // หรือถ้าต้องการลบ Match สำรองทิ้งไปเลยก็ได้ แต่ในที่นี้แค่เคลียร์คนออกเพื่อให้ทีมว่าง
            // _context.Matches.Remove(reserveMatch); 

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

            // 1. หา Staged Match เป้าหมาย (Target)
            var targetMatch = session.Matches.FirstOrDefault(m => m.Status == 4 && m.CourtNumber == dto.TargetCourtIdentifier);
            if (targetMatch == null)
            {
                // ถ้ายังไม่มี ให้สร้างใหม่
                targetMatch = new Match
                {
                    SessionId = sessionId,
                    CourtNumber = dto.TargetCourtIdentifier,
                    Status = 4, // FIX: 4 = Staged
                    CreatedDate = DateTime.UtcNow,
                    MatchPlayers = new List<MatchPlayer>()
                };
                _context.Matches.Add(targetMatch);
            }

            // 2. วนลูปย้ายผู้เล่นทีละคน
            foreach (var playerDto in dto.Players)
            {
                // FIX: แปลง ParticipantId เป็น UserId สำหรับ Member
                int? userId = null;
                int? walkinId = null;
                bool isMember = string.Equals(playerDto.Type, "Member", StringComparison.OrdinalIgnoreCase);

                if (isMember)
                {
                    var sp = await _context.SessionParticipants.FindAsync(playerDto.Id);
                    userId = sp?.UserId;
                    if (userId == null) continue; // ข้ามถ้าหา Member ไม่เจอ
                }
                else
                {
                    walkinId = playerDto.Id;
                }

                // 2.1 ตรวจสอบว่าผู้เล่นอยู่ในเป้าหมายแล้วหรือยัง
                bool alreadyInTarget = targetMatch.MatchPlayers.Any(p =>
                    (isMember && p.UserId == userId) ||
                    (!isMember && p.WalkinId == walkinId));
                
                if (alreadyInTarget) continue;

                // 2.2 ลบผู้เล่นออกจากที่เดิม (Staged Match อื่นๆ)
                var existingEntry = session.Matches
                    .Where(m => m.Status == 4) // หาเฉพาะใน Staged (Status 4)
                    .SelectMany(m => m.MatchPlayers)
                    .FirstOrDefault(p =>
                        (isMember && p.UserId == userId) ||
                        (!isMember && p.WalkinId == walkinId));

                if (existingEntry != null)
                {
                    _context.MatchPlayers.Remove(existingEntry);
                    
                    // --- FIX: ลบออกจาก List ในหน่วยความจำด้วย เพื่อให้ Count อัปเดตทันที ---
                    var parentMatch = session.Matches.FirstOrDefault(m => m.MatchId == existingEntry.MatchId);
                    if (parentMatch != null)
                    {
                        parentMatch.MatchPlayers.Remove(existingEntry);
                    }
                }

                // 2.3 เพิ่มผู้เล่นไปยังเป้าหมาย (ถ้ายังไม่เต็ม 4 คน)
                if (targetMatch.MatchPlayers.Count < 4)
                {
                    // กำหนดทีม A หรือ B ตามจำนวนคนที่มีอยู่
                    string team = targetMatch.MatchPlayers.Count < 2 ? "A" : "B";
                    
                    targetMatch.MatchPlayers.Add(new MatchPlayer
                    {
                        UserId = userId,
                        WalkinId = walkinId,
                        Team = team
                    });
                }
                else
                {
                    // ถ้าเป้าหมายเต็มแล้ว ผู้เล่นจะถูกลบจากที่เดิมแต่ไม่เข้าที่ใหม่ 
                    // (เท่ากับกลับไป Waiting List โดยอัตโนมัติ)
                }
            }

            // ลบ Match ที่ว่างเปล่าทิ้ง (Cleanup)
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
}