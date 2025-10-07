using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Services
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
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newSession = new GameSession
                {
                    CreatedByUserId = organizerUserId,
                    GroupName = dto.GroupName,
                    VenueId = dto.VenueId,
                    SessionDate = DateOnly.FromDateTime(dto.SessionDate),
                    StartTime = TimeOnly.FromTimeSpan(dto.SessionDate.TimeOfDay),
                    EndTime = TimeOnly.FromTimeSpan(dto.SessionDate.TimeOfDay.Add(dto.Duration)),
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

                return (await GetSessionByIdAsync(newSession.SessionId))!;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ManageGameSessionDto?> GetSessionByIdAsync(int sessionId)
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
                .Select(p => new ParticipantDto
                {
                    ParticipantId = p.ParticipantId,
                    ParticipantType = "Member",
                    UserId = p.UserId,
                    Nickname = p.User.UserProfile!.Nickname,
                    FullName = p.User.UserProfile.FirstName + " " + p.User.UserProfile.LastName,
                    Gender = p.User.UserProfile.Gender,
                    ProfilePhotoUrl = p.User.UserProfile.ProfilePhotoUrl,
                    SkillLevelId = p.SkillLevelId,
                    SkillLevelName = p.SkillLevel!.LevelName,
                    SkillLevelColor = p.SkillLevel.ColorHexCode,
                    Status = p.Status ?? 1,
                    CheckinTime = p.CheckinTime
                })
                .ToListAsync();

            
            var walkinGuests = await _context.SessionWalkinGuests
                .Where(g => g.SessionId == sessionId)
                .Include(g => g.SkillLevel)
                .Select(g => new ParticipantDto
                {
                    ParticipantId = g.WalkinId,
                    ParticipantType = "Guest",
                    UserId = null,
                    Nickname = g.GuestName,
                    FullName = null,
                    Gender = g.Gender,
                    ProfilePhotoUrl = null, // Walk-in ไม่มีรูปโปรไฟล์ในระบบ
                    SkillLevelId = g.SkillLevelId,
                    SkillLevelName = g.SkillLevel!.LevelName,
                    SkillLevelColor = g.SkillLevel.ColorHexCode,
                    Status = g.Status ?? 1,
                    CheckinTime = g.CheckinTime
                })
                .ToListAsync();

            
            session.Participants.AddRange(registeredParticipants);
            session.Participants.AddRange(walkinGuests);

            
            session.Participants = session.Participants.OrderBy(p => p.Status).ThenBy(p => p.ParticipantId).ToList();

            return session;
        }

        public async Task<IEnumerable<GameSessionSummaryDto>> GetUpcomingSessionsAsync()
        {
            // ดึงวันที่ปัจจุบันในรูปแบบ DateOnly
            var today = DateOnly.FromDateTime(DateTime.Now);

            return await _context.GameSessions
                // แก้ไขเงื่อนไขการเปรียบเทียบตรงนี้
                .Where(s => s.SessionDate >= today && s.Status == 1)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .Select(s => new GameSessionSummaryDto
                {
                    SessionId = s.SessionId,
                    GroupName = s.GroupName,
                    SessionStart = s.SessionDate.ToDateTime(s.StartTime), // แปลงกลับเป็น DateTime เพื่อแสดงผล
                    VenueName = s.Venue.VenueName,
                    MaxParticipants = s.MaxParticipants,
                    CurrentParticipants = s.SessionParticipants.Count(p => p.Status == 1)
                })
                .ToListAsync();
        }


        public async Task<IEnumerable<GameSessionSummaryDto>> GetMyCreatedSessionsAsync(int organizerUserId)
        {
            return await _context.GameSessions
               .Where(s => s.CreatedByUserId == organizerUserId)
               .Include(s => s.Venue)
               .Include(s => s.SessionParticipants)
               .OrderByDescending(s => s.SessionDate)
               .ThenByDescending(s => s.StartTime)
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

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                sessionToUpdate.GroupName = dto.GroupName;
                sessionToUpdate.VenueId = dto.VenueId;
                sessionToUpdate.SessionDate = DateOnly.FromDateTime(dto.SessionDate);
                sessionToUpdate.StartTime = TimeOnly.FromTimeSpan(dto.SessionDate.TimeOfDay);
                sessionToUpdate.EndTime = TimeOnly.FromTimeSpan(dto.SessionDate.TimeOfDay.Add(dto.Duration));
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

                if (dto.FacilityIds != null && dto.FacilityIds.Any())
                {
                    _context.GameSessionFacilities.RemoveRange(sessionToUpdate.GameSessionFacilities);
                    var newFacilities = dto.FacilityIds.Select(id => new GameSessionFacility
                    {
                        SessionId = sessionId,
                        FacilityId = id,
                        CreatedBy = organizerUserId
                    });
                    await _context.GameSessionFacilities.AddRangeAsync(newFacilities);
                }


                if (dto.PhotoUrls != null && dto.PhotoUrls.Any())
                {
                    _context.GameSessionPhotos.RemoveRange(sessionToUpdate.GameSessionPhotos);
                    var newPhotos = dto.PhotoUrls.Select((url, i) => new GameSessionPhoto
                    {
                        SessionId = sessionId,
                        PhotoUrl = url,
                        DisplayOrder = (byte)(i + 1),
                        CreatedBy = organizerUserId
                    });
                    await _context.GameSessionPhotos.AddRangeAsync(newPhotos);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await GetSessionByIdAsync(sessionId);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
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

        public async Task<ManageGameSessionDto> DuplicateSessionForNextWeekAsync(int oldSessionId, int organizerUserId)
        {
            var oldSession = await _context.GameSessions
                .Include(s => s.GameSessionFacilities)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == oldSessionId && s.CreatedByUserId == organizerUserId);

            if (oldSession == null) throw new KeyNotFoundException("Session not found or you do not own this session.");

            var dto = new SaveGameSessionDto(
                oldSession.GroupName,
                oldSession.VenueId,
                  oldSession.SessionDate.AddDays(7).ToDateTime(TimeOnly.MinValue), // *** บวกไป 7 วัน ***
                oldSession.EndTime - oldSession.StartTime,
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
    }
}