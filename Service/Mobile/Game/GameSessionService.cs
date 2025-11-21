using System.Globalization;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Service.Mobile.Game
{
    public class GameSessionService : IGameSessionService
    {
        private readonly BadmintonDbContext _context;

        public GameSessionService(BadmintonDbContext context)
        {
            _context = context;
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
                    return (await GetSessionForManageViewAsync(newSession.SessionId))!;
                }
                catch (Exception ex)
                {
                    // ไม่ต้อง Rollback ตรงนี้แล้ว เพราะถ้าเกิด Exception ก่อน Commit, Transaction จะ Rollback เอง
                    // await transaction.RollbackAsync(); // << เอาออกได้
                    throw; // ปล่อยให้ strategy จัดการ Error หรือลองใหม่
                }
            }); // <-- ปิด ExecuteAsync
        }

        private async Task<ManageGameSessionDto?> GetSessionForManageViewAsync(int sessionId)
        {
            var session = await _context.GameSessions
                .Where(s => s.SessionId == sessionId)
                .Include(s => s.Venue)
                .Include(s => s.ShuttlecockModel)
                    .ThenInclude(m => m!.Brand)
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
                    ShuttlecockBrandName = s.ShuttlecockModel!.Brand.BrandName,
                    ShuttlecockModelName = s.ShuttlecockModel.ModelName,
                    ShuttlecockCostPerUnit = s.ShuttlecockCostPerUnit,
                    CourtFeePerPerson = s.CourtFeePerPerson,
                    MaxParticipants = s.MaxParticipants,
                    Notes = s.Notes,
                    PhotoUrls = s.GameSessionPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.PhotoUrl).ToList()
                })
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return null;
            }

            var registeredParticipants = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId)
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .Select(p => CreateParticipantDto(p))
                .ToListAsync();

            var walkinGuests = await _context.SessionWalkinGuests
                .Where(g => g.SessionId == sessionId)
                .Include(g => g.SkillLevel)
                .Select(g => CreateParticipantDto(g))
                .ToListAsync();


            session.Participants.AddRange(registeredParticipants);
            session.Participants.AddRange(walkinGuests);

            session.Participants = session.Participants.OrderBy(p => p.Status).ThenBy(p => p.ParticipantId).ToList();

            return session;
        }

        public async Task<EditGameSessionDto?> GetSessionForEditAsync(int sessionId)
        {
            var session = await _context.GameSessions
                .Where(s => s.SessionId == sessionId)
                .Include(s => s.Venue)
                .Include(s => s.ShuttlecockModel) // <-- เพิ่ม Include
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

            var registeredParticipants = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId)
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .Select(p => CreateParticipantDto(p))
                .ToListAsync();

            var walkinGuests = await _context.SessionWalkinGuests
                .Where(g => g.SessionId == sessionId)
                .Include(g => g.SkillLevel)
                .Select(g => CreateParticipantDto(g))
                .ToListAsync();

            session.Participants.AddRange(registeredParticipants);
            session.Participants.AddRange(walkinGuests);

            session.Participants = session.Participants.OrderBy(p => p.Status).ThenBy(p => p.ParticipantId).ToList();

            return session;
        }

        private static ParticipantDto CreateParticipantDto(SessionParticipant p)
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
                CheckinTime = p.CheckinTime
            };
        }

        private static ParticipantDto CreateParticipantDto(SessionWalkinGuest g)
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
                CheckinTime = g.CheckinTime
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

                    return await GetSessionForManageViewAsync(sessionId);
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
            var activeParticipants = session.SessionParticipants.Count(p => p.Status == 1);
            if (activeParticipants < session.MaxParticipants)
            {
                // ยังไม่เต็ม -> เข้าร่วมเป็นตัวจริง
                newStatus = 1;
                statusMessage = "Joined successfully.";
            }
            else
            {
                // ก๊วนเต็มแล้ว -> ไปต่อคิวสำรอง
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

            // 2. **Logic สำคัญ:** ตรวจสอบว่ามีคิวสำรองหรือไม่
            var session = await _context.GameSessions
                .Include(s => s.SessionParticipants)
                .FirstAsync(s => s.SessionId == sessionId);

            var activeParticipants = session.SessionParticipants.Count(p => p.Status == 1);

            if (activeParticipants < session.MaxParticipants)
            {
                // 3. ถ้ามีที่ว่าง ให้ดึง "คิวแรกสุด" (Status=2) ขึ้นมา
                var nextInLine = session.SessionParticipants
                    .Where(p => p.Status == 2)
                    .OrderBy(p => p.JoinedDate)
                    .FirstOrDefault();

                if (nextInLine != null)
                {
                    // 4. เลื่อนขั้นคิวสำรองให้เป็นตัวจริง
                    nextInLine.Status = 1;
                    // TODO: ณ จุดนี้ ควรส่ง Notification ไปหา nextInLine.UserId ว่า "คุณได้เป็นตัวจริงแล้ว"
                }
            }

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
            if (currentParticipants >= session.MaxParticipants)
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
        .Where(s => s.CreatedByUserId == organizerUserId && s.SessionDate >= today && s.Status != 3)
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

        public async Task<IEnumerable<GameSessionSummaryDto>> GetMyPastSessionsAsync(int organizerUserId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            return await _context.GameSessions
               .Where(s => s.CreatedByUserId == organizerUserId && s.SessionDate < today) // << กรองเฉพาะอดีต
               .Include(s => s.Venue)
               .Include(s => s.SessionParticipants)
               .OrderByDescending(s => s.SessionDate) // เรียงจากล่าสุดไปเก่าสุด
               .ThenByDescending(s => s.StartTime)
               .Take(20) // ดึงมาแค่ 20 ก๊วนล่าสุดก็พอ
               .Select(s => new GameSessionSummaryDto
               {
                   SessionId = s.SessionId,
                   GroupName = s.GroupName,
                   SessionStart = s.SessionDate.ToDateTime(s.StartTime),
                   VenueName = s.Venue.VenueName,
                   MaxParticipants = s.MaxParticipants,
                   CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1)
               })
               .ToListAsync();
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
    }
}