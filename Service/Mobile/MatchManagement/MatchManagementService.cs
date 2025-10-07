using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Services
{
    public class MatchManagementService : IMatchManagementService
    {
        private readonly BadmintonDbContext _context;
        public MatchManagementService(BadmintonDbContext context) { _context = context; }

        public async Task<LiveSessionStateDto?> GetLiveStateAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId) return null;

            var result = new LiveSessionStateDto();

            // 1. ดึงแมตช์ที่กำลังเล่นอยู่
            var activeMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 1)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User!.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin)
                .ToListAsync();

            var playersInMatchIds = new List<int?>();
            var walkinsInMatchIds = new List<int?>();

            foreach (var match in activeMatches)
            {
                // สร้าง DTO สำหรับแมตช์ปัจจุบัน
                var matchDto = new CurrentlyPlayingMatchDto
                {
                    MatchId = match.MatchId,
                    CourtNumber = match.CourtNumber,
                    StartTime = match.StartTime ?? DateTime.UtcNow // ใช้เวลาปัจจุบันถ้า StartTime เป็น null
                };

                // วนลูปผู้เล่นทั้ง 4 คนในแมตช์
                foreach (var player in match.MatchPlayers)
                {
                    // สร้าง DTO สำหรับผู้เล่นแต่ละคน
                    var playerDto = new PlayerInMatchDto();

                    // ตรวจสอบว่าเป็น Member หรือ Guest
                    if (player.UserId.HasValue)
                    {
                        playerDto.UserId = player.UserId;
                        playerDto.Nickname = player.User?.UserProfile?.Nickname ?? "N/A"; // ดึงชื่อเล่นจาก Profile
                        playersInMatchIds.Add(player.UserId.Value); // เก็บ ID ไว้เพื่อใช้กรอง
                    }
                    else if (player.WalkinId.HasValue)
                    {
                        playerDto.WalkinId = player.WalkinId;
                        playerDto.Nickname = player.Walkin?.GuestName ?? "N/A"; // ดึงชื่อจาก Guest
                        walkinsInMatchIds.Add(player.WalkinId.Value); // เก็บ ID ไว้เพื่อใช้กรอง
                    }

                    // แบ่งผู้เล่นเข้าทีม A หรือ B
                    if (player.Team == "A")
                    {
                        matchDto.TeamA.Add(playerDto);
                    }
                    else if (player.Team == "B")
                    {
                        matchDto.TeamB.Add(playerDto);
                    }
                }

                // เพิ่มข้อมูลแมตช์ที่สมบูรณ์แล้วเข้าไปในผลลัพธ์
                result.CurrentlyPlaying.Add(matchDto);
            }

            // 2. ดึงผู้เล่นทั้งหมดที่เช็คอินแล้ว แต่ยังไม่ Checkout
            var waitingMembers = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId && p.CheckinTime != null && p.CheckoutTime == null && !playersInMatchIds.Contains(p.UserId))
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .Select(p => new WaitingPlayerDto
                {
                    ParticipantId = p.ParticipantId,            // ID ของรายการลงทะเบียน
                    ParticipantType = "Member",                 // ประเภทผู้เล่น
                    Nickname = p.User!.UserProfile!.Nickname,   // ชื่อเล่น (จาก UserProfile)
                    ProfilePhotoUrl = p.User.UserProfile.ProfilePhotoUrl, // รูปโปรไฟล์ (จาก UserProfile)
                    SkillLevelName = p.SkillLevel!.LevelName,    // ชื่อระดับมือ (จาก OrganizerSkillLevel)
                    SkillLevelColor = p.SkillLevel.ColorHexCode, // สีของระดับมือ
                    CheckedInTime = p.CheckinTime!.Value          // เวลาที่เช็คอิน
                })
                .ToListAsync();

            var waitingGuests = await _context.SessionWalkinGuests
                 .Where(g => g.SessionId == sessionId && g.CheckinTime != null && g.CheckoutTime == null && !walkinsInMatchIds.Contains(g.WalkinId))
                 .Include(g => g.SkillLevel)
                 .Select(g => new WaitingPlayerDto
                 {
                     ParticipantId = g.WalkinId,                 // ID ของรายการ Walk-in
                     ParticipantType = "Guest",                  // ประเภทผู้เล่น
                     Nickname = g.GuestName,                     // ชื่อเล่น (จากตาราง Walkin)
                     ProfilePhotoUrl = null,                     // Walk-in ไม่มีรูปโปรไฟล์ในระบบ
                     SkillLevelName = g.SkillLevel!.LevelName,    // ชื่อระดับมือ
                     SkillLevelColor = g.SkillLevel.ColorHexCode, // สีของระดับมือ
                     CheckedInTime = g.CheckinTime!.Value          // เวลาที่เช็คอิน
                 })
                 .ToListAsync();

            result.WaitingPool.AddRange(waitingMembers);
            result.WaitingPool.AddRange(waitingGuests);

            return result;
        }

        public async Task<CurrentlyPlayingMatchDto> StartMatchAsync(int sessionId, int organizerUserId, CreateMatchDto dto)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId) throw new Exception("Unauthorized");

            var match = new Models.Match
            {
                SessionId = sessionId,
                CourtNumber = dto.CourtNumber,
                StartTime = DateTime.UtcNow,
                Status = 1, // กำลังเล่น
                CreatedBy = organizerUserId
            };
            await _context.Matches.AddAsync(match);
            await _context.SaveChangesAsync(); // Save to get MatchID

            var matchPlayers = new List<Models.MatchPlayer>();
            Action<ParticipantIdentifierDto, string> addPlayer = (p, team) =>
            {
                var mp = new Models.MatchPlayer { MatchId = match.MatchId, Team = team };
                if (p.Type == "Member") mp.UserId = p.Id;
                else mp.WalkinId = p.Id;
                matchPlayers.Add(mp);
            };

            dto.TeamA.ForEach(p => addPlayer(p, "A"));
            dto.TeamB.ForEach(p => addPlayer(p, "B"));

            await _context.MatchPlayers.AddRangeAsync(matchPlayers);
            await _context.SaveChangesAsync();

            // TODO: Map to CurrentlyPlayingMatchDto and return
            return new CurrentlyPlayingMatchDto { MatchId = match.MatchId };
        }

        public async Task<bool> SubmitPlayerResultAsync(int matchId, int userId, SubmitResultDto dto)
        {
            var matchPlayer = await _context.MatchPlayers
                .FirstOrDefaultAsync(mp => mp.MatchId == matchId && mp.UserId == userId);

            if (matchPlayer == null) return false;

            matchPlayer.Result = (byte)dto.Result;
            matchPlayer.Notes = dto.Notes;
            matchPlayer.UpdatedBy = userId;
            matchPlayer.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<BillSummaryDto?> CheckoutParticipantAsync(int participantId, string participantType, int organizerUserId)
        {
            // 1. ค้นหา GameSession เพื่อตรวจสอบสิทธิ์ความเป็นเจ้าของ
            GameSession? session = null;
            int? userId = null;
            int? walkinId = null;

            if (participantType == "member")
            {
                var participant = await _context.SessionParticipants
                    .Include(p => p.Session)
                    .FirstOrDefaultAsync(p => p.ParticipantId == participantId);

                if (participant == null || participant.Session.CreatedByUserId != organizerUserId) return null;

                // อัปเดตเวลา Checkout
                participant.CheckoutTime = DateTime.UtcNow;
                session = participant.Session;
                userId = participant.UserId;
            }
            else if (participantType == "guest")
            {
                var guest = await _context.SessionWalkinGuests
                    .Include(g => g.Session)
                    .FirstOrDefaultAsync(g => g.WalkinId == participantId);

                if (guest == null || guest.Session.CreatedByUserId != organizerUserId) return null;

                // อัปเดตเวลา Checkout
                guest.CheckoutTime = DateTime.UtcNow;
                session = guest.Session;
                walkinId = guest.WalkinId;
            }
            else
            {
                return null; // participantType ไม่ถูกต้อง
            }

            // เริ่ม Transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. คำนวณค่าใช้จ่าย
                var lineItems = new List<BillLineItem>();
                decimal totalAmount = 0;

                // เพิ่มค่าคอร์ท (ถ้ามี)
                if (session.CourtFeePerPerson.HasValue && session.CourtFeePerPerson > 0)
                {
                    var amount = session.CourtFeePerPerson.Value;
                    lineItems.Add(new BillLineItem { Description = "ค่าคอร์ท", Amount = amount });
                    totalAmount += amount;
                }

                // เพิ่มค่าลูกแบด (ถ้ามี)
                if (session.ShuttlecockFeePerPerson.HasValue && session.ShuttlecockFeePerPerson > 0)
                {
                    var amount = session.ShuttlecockFeePerPerson.Value;
                    lineItems.Add(new BillLineItem { Description = "ค่าลูกแบด", Amount = amount });
                    totalAmount += amount;
                }

                // 3. สร้างใบแจ้งหนี้หลัก (ParticipantBill)
                var newBill = new ParticipantBill
                {
                    SessionId = session.SessionId,
                    UserId = userId,
                    WalkinId = walkinId,
                    TotalAmount = totalAmount,
                    Status = 1, // 1=ยังไม่จ่าย
                    CreatedDate = DateTime.UtcNow
                };
                await _context.ParticipantBills.AddAsync(newBill);
                await _context.SaveChangesAsync(); // บันทึกเพื่อเอา BillID

                // 4. เพิ่มรายการลงในใบแจ้งหนี้ (BillLineItems)
                foreach (var item in lineItems)
                {
                    item.BillId = newBill.BillId;
                }
                await _context.BillLineItems.AddRangeAsync(lineItems);

                // 5. บันทึกการเปลี่ยนแปลงทั้งหมด
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 6. สร้าง DTO เพื่อส่งกลับ
                var billSummary = new BillSummaryDto
                {
                    BillId = newBill.BillId,
                    TotalAmount = newBill.TotalAmount,
                    LineItems = lineItems.Select(li => new BillLineItemDto { Description = li.Description, Amount = li.Amount }).ToList()
                };

                return billSummary;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw; // ปล่อยให้ Middleware จัดการ Error
            }
        }

    }
}