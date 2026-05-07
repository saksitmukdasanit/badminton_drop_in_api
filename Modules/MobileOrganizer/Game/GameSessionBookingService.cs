using System.Data;
using DropInBadAPI.Constants;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Service.Mobile.Game;

public class GameSessionBookingService : IGameSessionBookingService
{
    private readonly BadmintonDbContext _context;
    private readonly INotificationService _notificationService;

    public GameSessionBookingService(BadmintonDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
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

        if (currentParticipants >= session.MaxParticipants || waitlistedParticipants > 0)
        {
            newStatus = 2;
        }
        else
        {
            newStatus = 1;
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

        var skillLevel = dto.SkillLevelId.HasValue
            ? await _context.OrganizerSkillLevels.FindAsync(dto.SkillLevelId.Value)
            : null;

        newGuest.SkillLevel = skillLevel;

        return (ParticipantDtoMapper.FromGuest(newGuest), string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> RemoveParticipantAsync(int sessionId, string participantType, int participantId, int organizerUserId)
    {
        var session = await _context.GameSessions
            .Include(s => s.SessionParticipants)
            .Include(s => s.SessionWalkinGuests)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

        if (session == null) return (false, "Session not found or permission denied.");

        if (participantType.Equals(ParticipantTypes.Member, StringComparison.OrdinalIgnoreCase))
        {
            var p = session.SessionParticipants.FirstOrDefault(x => x.ParticipantId == participantId);
            if (p == null) return (false, "Participant not found.");

            p.Status = 3;
            p.CheckoutTime = DateTime.UtcNow;

            await _notificationService.SendNotificationAsync(
                p.UserId,
                "คุณถูกนำออกจากก๊วน",
                $"คุณถูกนำออกจากก๊วน '{session.GroupName}' โดยผู้จัด",
                "REMOVED_FROM_SESSION",
                sessionId);
        }
        else if (participantType.Equals(ParticipantTypes.Guest, StringComparison.OrdinalIgnoreCase))
        {
            var g = session.SessionWalkinGuests.FirstOrDefault(x => x.WalkinId == participantId);
            if (g == null) return (false, "Guest not found.");

            g.Status = 3;
            g.CheckoutTime = DateTime.UtcNow;
        }
        else
        {
            return (false, "Invalid participant type.");
        }

        await _context.SaveChangesAsync();
        return (true, "Participant removed successfully.");
    }

    public async Task<(bool Success, string ErrorMessage)> PromoteWaitlistedParticipantAsync(int sessionId, string participantType, int participantId, int organizerUserId)
    {
        // CONCURRENCY: ห่อด้วย Serializable transaction เพื่อกัน 2 คำขอพร้อมกันเลื่อนเกิน MaxParticipants
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var session = await _context.GameSessions
                .Include(s => s.SessionParticipants)
                .Include(s => s.SessionWalkinGuests)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

            if (session == null)
            {
                await tx.RollbackAsync();
                return (false, "Session not found or permission denied.");
            }

            int currentCount = session.SessionParticipants.Count(p => p.Status == 1) +
                               session.SessionWalkinGuests.Count(g => g.Status == 1);

            if (currentCount >= session.MaxParticipants)
            {
                await tx.RollbackAsync();
                return (false, "Session is full. Cannot promote participant.");
            }

            int? userIdToNotify = null;
            string groupName = session.GroupName;

            if (participantType.Equals(ParticipantTypes.Member, StringComparison.OrdinalIgnoreCase))
            {
                var p = session.SessionParticipants.FirstOrDefault(x => x.ParticipantId == participantId);
                if (p == null) { await tx.RollbackAsync(); return (false, "Participant not found."); }
                if (p.Status != 2) { await tx.RollbackAsync(); return (false, "Participant is not in waitlist."); }
                p.Status = 1;
                userIdToNotify = p.UserId;
            }
            else if (participantType.Equals(ParticipantTypes.Guest, StringComparison.OrdinalIgnoreCase))
            {
                var g = session.SessionWalkinGuests.FirstOrDefault(x => x.WalkinId == participantId);
                if (g == null) { await tx.RollbackAsync(); return (false, "Guest not found."); }
                if (g.Status != 2) { await tx.RollbackAsync(); return (false, "Guest is not in waitlist."); }
                g.Status = 1;
            }
            else
            {
                await tx.RollbackAsync();
                return (false, "Invalid participant type.");
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            // ส่ง notification หลัง commit เพื่อกันส่งผิดเมื่อ rollback
            if (userIdToNotify.HasValue)
            {
                await _notificationService.SendNotificationAsync(
                    userIdToNotify.Value,
                    "คุณได้เป็นผู้เล่นตัวจริงแล้ว!",
                    $"คุณได้รับการเลื่อนสถานะเป็นผู้เล่นตัวจริงในก๊วน '{groupName}'",
                    "PROMOTED_TO_ACTIVE",
                    sessionId);
            }

            return (true, "Participant promoted successfully.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
