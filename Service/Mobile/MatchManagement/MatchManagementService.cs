using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Hubs;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DropInBadAPI.Services
{
    public class MatchManagementService : IMatchManagementService
    {
        private readonly BadmintonDbContext _context;
        private readonly IHubContext<ManagementGameHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;

        public MatchManagementService(
            BadmintonDbContext context, 
            IHubContext<ManagementGameHub> hubContext,
            IServiceProvider serviceProvider) {
            _context = context;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
        }

        public async Task<LiveSessionStateDto?> GetLiveStateAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId) return null;

            // 1. ดึงข้อมูลแมตช์ที่กำลังเล่นอยู่ทั้งหมดใน Session นี้ (Status = 1)
            var activeMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 1)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User.SessionParticipants.Where(sp => sp.SessionId == sessionId)).ThenInclude(sp => sp.SkillLevel)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin.SkillLevel)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin)
                .ToListAsync();

            // 2. ดึงข้อมูลแมตช์ที่จัดเตรียมไว้ (Status = 4)
            var stagedMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId && m.Status == 4)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User.SessionParticipants.Where(sp => sp.SessionId == sessionId)).ThenInclude(sp => sp.SkillLevel)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin.SkillLevel)
                .OrderBy(m => m.CreatedDate)
                .ToListAsync();

            // 3. สร้าง List ของชื่อสนามจริง (Official Courts) ขึ้นมาก่อน เพื่อใช้ตรวจสอบ
            var courtIdentifiers = session.CourtNumbers?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(c => c.Trim())
                                    .ToList();

            if (courtIdentifiers == null || !courtIdentifiers.Any())
            {
                courtIdentifiers = Enumerable.Range(1, session.NumberOfCourts ?? 1).Select(i => i.ToString()).ToList();
            }
            
            // สร้าง HashSet เพื่อให้ตรวจสอบชื่อสนามได้เร็วและแม่นยำ (Case-insensitive)
            var validCourtSet = new HashSet<string>(courtIdentifiers, StringComparer.OrdinalIgnoreCase);

            // 4. แปลงแมตช์ที่กำลังเล่นอยู่ให้เป็น Dictionary
            var activeMatchesMap = activeMatches.ToDictionary(
                match => match.CourtNumber ?? "", // Handle null key
                match => new CurrentlyPlayingMatchDto
                {
                    MatchId = match.MatchId,
                    CourtNumber = match.CourtNumber,
                    StartTime = match.StartTime,
                    TeamA = match.MatchPlayers.Where(p => p.Team == "A").Select(p =>
                    {
                        var sessionParticipant = p.User?.SessionParticipants.FirstOrDefault();
                        return new PlayerInMatchDto
                        {
                            UserId = p.UserId,
                            WalkinId = p.WalkinId,
                            Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                            GenderName = p.UserId.HasValue ? (p.User.UserProfile.Gender == 1 ? "ชาย" : p.User.UserProfile.Gender == 2 ? "หญิง" : "อื่นๆ") : (p.Walkin.Gender == 1 ? "ชาย" : p.Walkin.Gender == 2 ? "หญิง" : "อื่นๆ"),
                            SkillLevelId = p.UserId.HasValue ? sessionParticipant?.SkillLevelId : p.Walkin?.SkillLevelId,
                            SkillLevelName = p.UserId.HasValue ? sessionParticipant?.SkillLevel?.LevelName : p.Walkin?.SkillLevel?.LevelName,
                            SkillLevelColor = p.UserId.HasValue ? sessionParticipant?.SkillLevel?.ColorHexCode : p.Walkin?.SkillLevel?.ColorHexCode
                        };
                    }).ToList(),
                    TeamB = match.MatchPlayers.Where(p => p.Team == "B").Select(p =>
                    {
                        var sessionParticipant = p.User?.SessionParticipants.FirstOrDefault();
                        return new PlayerInMatchDto
                        {
                            UserId = p.UserId,
                            WalkinId = p.WalkinId,
                            Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                            GenderName = p.UserId.HasValue ? (p.User.UserProfile.Gender == 1 ? "ชาย" : p.User.UserProfile.Gender == 2 ? "หญิง" : "อื่นๆ") : (p.Walkin.Gender == 1 ? "ชาย" : p.Walkin.Gender == 2 ? "หญิง" : "อื่นๆ"),
                            SkillLevelId = p.UserId.HasValue ? sessionParticipant?.SkillLevelId : p.Walkin?.SkillLevelId,
                            SkillLevelName = p.UserId.HasValue ? sessionParticipant?.SkillLevel?.LevelName : p.Walkin?.SkillLevel?.LevelName,
                            SkillLevelColor = p.UserId.HasValue ? sessionParticipant?.SkillLevel?.ColorHexCode : p.Walkin?.SkillLevel?.ColorHexCode
                        };
                    }).ToList()
                });

            // 5. แยก Staged Matches โดยใช้ Logic String Whitelist
            
            // กลุ่ม A: ลงสนามจริง (CourtNumber มีค่า และ "มีชื่ออยู่ในรายการสนามของ Session")
            var stagedMatchesForCourts = stagedMatches
                .Where(m => !string.IsNullOrEmpty(m.CourtNumber) && validCourtSet.Contains(m.CourtNumber))
                .GroupBy(m => m.CourtNumber!)
                .ToDictionary(g => g.Key, g => g.First());

            // กลุ่ม B: ทีมสำรอง (CourtNumber เป็น Null หรือ "ไม่มีชื่อในสนามจริง" เช่น "-1", "-2")
            var generalStagedMatches = stagedMatches
                .Where(m => string.IsNullOrEmpty(m.CourtNumber) || !validCourtSet.Contains(m.CourtNumber))
                .ToList();

            // Helper function for mapping
            Func<MatchPlayer, PlayerInMatchDto> createStagedPlayerDto = p =>
            {
                var sessionParticipant = p.User?.SessionParticipants.FirstOrDefault();
                return new PlayerInMatchDto
                {
                    UserId = p.UserId,
                    WalkinId = p.WalkinId,
                    Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                    ProfilePhotoUrl = p.UserId.HasValue ? p.User?.UserProfile?.ProfilePhotoUrl : null,
                    GenderName = p.UserId.HasValue ? (p.User.UserProfile.Gender == 1 ? "ชาย" : p.User.UserProfile.Gender == 2 ? "หญิง" : "อื่นๆ") : (p.Walkin.Gender == 1 ? "ชาย" : p.Walkin.Gender == 2 ? "หญิง" : "อื่นๆ"),
                    SkillLevelId = p.UserId.HasValue ? sessionParticipant?.SkillLevelId : p.Walkin?.SkillLevelId,
                    SkillLevelName = p.UserId.HasValue ? sessionParticipant?.SkillLevel?.LevelName : p.Walkin?.SkillLevel?.LevelName,
                    SkillLevelColor = p.UserId.HasValue ? sessionParticipant?.SkillLevel?.ColorHexCode : p.Walkin?.SkillLevel?.ColorHexCode
                };
            };

            // รวมแมตช์ที่จัดรอในสนาม (Staged - Matches on Courts) เข้าไปแสดงผลทับสนาม
            foreach (var stagedMatch in stagedMatchesForCourts.Values)
            {
                if (stagedMatch.CourtNumber != null)
                {
                    activeMatchesMap[stagedMatch.CourtNumber] = new CurrentlyPlayingMatchDto
                    {
                        MatchId = stagedMatch.MatchId,
                        CourtNumber = stagedMatch.CourtNumber,
                        StartTime = null, // ยังไม่เริ่ม
                        TeamA = stagedMatch.MatchPlayers.Where(p => p.Team == "A").Select(createStagedPlayerDto).ToList(),
                        TeamB = stagedMatch.MatchPlayers.Where(p => p.Team == "B").Select(createStagedPlayerDto).ToList()
                    };
                }
            }

            var courtStatuses = courtIdentifiers.Select(identifier => new CourtStatusDto
            {
                CourtIdentifier = identifier,
                CurrentMatch = activeMatchesMap.TryGetValue(identifier, out var match) ? match : null
            }).ToList();

            // --- (ส่วน Waiting Pool / ID Filtering) ---
            var playersInMatchIds = activeMatches
                .SelectMany(m => m.MatchPlayers).Where(mp => mp.UserId.HasValue).Select(mp => mp.UserId).ToHashSet();
            var playersInStagedMatchIds = stagedMatches
                .SelectMany(m => m.MatchPlayers).Where(mp => mp.UserId.HasValue).Select(mp => mp.UserId).ToHashSet();
            var walkinsInMatchIds = activeMatches
                .SelectMany(m => m.MatchPlayers).Where(mp => mp.WalkinId.HasValue).Select(mp => mp.WalkinId).ToHashSet();
            var walkinsInStagedMatchIds = stagedMatches
                .SelectMany(m => m.MatchPlayers).Where(mp => mp.WalkinId.HasValue).Select(mp => mp.WalkinId).ToHashSet();

            var waitingMembers = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId && p.CheckinTime != null && p.CheckoutTime == null && !playersInMatchIds.Contains(p.UserId) && !playersInStagedMatchIds.Contains(p.UserId))
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .Select(p => new WaitingPlayerDto
                {
                    ParticipantId = p.ParticipantId,
                    ParticipantType = "Member",
                    Nickname = p.User.UserProfile.Nickname,
                    ProfilePhotoUrl = p.User.UserProfile.ProfilePhotoUrl,
                    GenderName = p.User.UserProfile.Gender == 1 ? "ชาย" : p.User.UserProfile.Gender == 2 ? "หญิง" : "อื่นๆ",
                    SkillLevelId = p.SkillLevel != null ? p.SkillLevel.SkillLevelId : null,
                    SkillLevelName = p.SkillLevel != null ? p.SkillLevel.LevelName : null,
                    SkillLevelColor = p.SkillLevel != null ? p.SkillLevel.ColorHexCode : null,
                    CheckedInTime = p.CheckinTime.Value
                })
                .ToListAsync();

            var waitingGuests = await _context.SessionWalkinGuests
                    .Where(g => g.SessionId == sessionId && g.CheckinTime != null && g.CheckoutTime == null && !walkinsInMatchIds.Contains(g.WalkinId) && !walkinsInStagedMatchIds.Contains(g.WalkinId))
                    .Include(g => g.SkillLevel)
                    .Select(g => new WaitingPlayerDto
                    {
                        ParticipantId = g.WalkinId,
                        ParticipantType = "Guest",
                        Nickname = g.GuestName,
                        ProfilePhotoUrl = null,
                        GenderName = g.Gender == 1 ? "ชาย" : g.Gender == 2 ? "หญิง" : "อื่นๆ",
                        SkillLevelId = g.SkillLevel != null ? g.SkillLevel.SkillLevelId : null,
                        SkillLevelName = g.SkillLevel != null ? g.SkillLevel.LevelName : null,
                        SkillLevelColor = g.SkillLevel != null ? g.SkillLevel.ColorHexCode : null,
                        CheckedInTime = g.CheckinTime.Value
                    })
                    .ToListAsync();

            var allWaitingPlayers = waitingMembers.Concat(waitingGuests).ToList();

            // 6. แปลง Staged Matches (ทีมสำรอง/General) เป็น DTO
            var stagedMatchesDto = generalStagedMatches.Select(match => new StagedMatchDto
            {
                MatchId = match.MatchId,
                CourtNumber = match.CourtNumber, // ส่งค่าเดิมกลับไป (เช่น "-1", "-2")
                TeamA = match.MatchPlayers.Where(p => p.Team == "A").Select(createStagedPlayerDto).ToList(),
                TeamB = match.MatchPlayers.Where(p => p.Team == "B").Select(createStagedPlayerDto).ToList()
            }).ToList();

            // --- จัดการชื่อซ้ำทั้งหมดใน Session ---
            // 1. รวบรวมผู้เล่นทั้งหมดจากทุกที่ (Courts, Staged, Waiting)
            var allPlayersInSession = new List<object>();
            allPlayersInSession.AddRange(courtStatuses.SelectMany(cs => cs.CurrentMatch?.TeamA ?? new List<PlayerInMatchDto>()));
            allPlayersInSession.AddRange(courtStatuses.SelectMany(cs => cs.CurrentMatch?.TeamB ?? new List<PlayerInMatchDto>()));
            allPlayersInSession.AddRange(stagedMatchesDto.SelectMany(sm => sm.TeamA));
            allPlayersInSession.AddRange(stagedMatchesDto.SelectMany(sm => sm.TeamB));
            allPlayersInSession.AddRange(allWaitingPlayers);

            // 2. สร้างฟังก์ชันสำหรับจัดการชื่อซ้ำ
            Action<IEnumerable<dynamic>> processDuplicateNames = (players) =>
            {
                var duplicateGroups = players
                    .Where(p => !string.IsNullOrEmpty(p.Nickname))
                    .GroupBy(p => p.Nickname)
                    .Where(g => g.Count() > 1);

                foreach (var group in duplicateGroups)
                {
                    int counter = 1;
                    foreach (var player in group)
                    {
                        player.Nickname = $"{player.Nickname} ({counter++})";
                    }
                }
            };

            // 3. เรียกใช้ฟังก์ชันกับผู้เล่นทั้งหมด
            processDuplicateNames(allPlayersInSession.Cast<dynamic>());

            var result = new LiveSessionStateDto
            {
                groupName = session.GroupName,
                Courts = courtStatuses,
                WaitingPool = allWaitingPlayers.OrderBy(p => p.CheckedInTime).ToList(),
                StagedMatches = stagedMatchesDto
            };

            return result;
        }

        public async Task<CurrentlyPlayingMatchDto> StartMatchAsync(int sessionId, int organizerUserId, CreateMatchDto dto)
        {
            // 1. ตรวจสอบสิทธิ์
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId) throw new Exception("Unauthorized");

            // 2. สร้างและบันทึก Match หลัก
            var match = new Match
            {
                SessionId = sessionId,
                CourtNumber = dto.CourtNumber,
                StartTime = DateTime.UtcNow,
                Status = 1, // 1=กำลังเล่น
                CreatedBy = organizerUserId,
            };
            await _context.Matches.AddAsync(match);
            await _context.SaveChangesAsync();

            // 3. ดึง UserId จาก ParticipantId สำหรับผู้เล่นที่เป็น Member
            var memberParticipantIds = dto.TeamA.Concat(dto.TeamB)
                .Where(p => p.Type == "Member")
                .Select(p => p.Id)
                .ToList();

            var memberUserIdMap = await _context.SessionParticipants
                .Where(sp => memberParticipantIds.Contains(sp.ParticipantId))
                .ToDictionaryAsync(sp => sp.ParticipantId, sp => sp.UserId);

            // 4. สร้างและบันทึกผู้เล่นใน Match (MatchPlayer)
            var matchPlayers = new List<MatchPlayer>();
            foreach (var p in dto.TeamA)
            {
                int? userId = p.Type == "Member" && memberUserIdMap.ContainsKey(p.Id) ? memberUserIdMap[p.Id] : null;
                int? walkinId = p.Type == "Guest" ? p.Id : null;
                matchPlayers.Add(new MatchPlayer { MatchId = match.MatchId, Team = "A", UserId = userId, WalkinId = walkinId });
            }
            foreach (var p in dto.TeamB)
            {
                int? userId = p.Type == "Member" && memberUserIdMap.ContainsKey(p.Id) ? memberUserIdMap[p.Id] : null;
                int? walkinId = p.Type == "Guest" ? p.Id : null;
                matchPlayers.Add(new MatchPlayer { MatchId = match.MatchId, Team = "B", UserId = userId, WalkinId = walkinId });
            }

            await _context.MatchPlayers.AddRangeAsync(matchPlayers);
            await _context.SaveChangesAsync();

            // 5. เตรียมข้อมูลเพื่อส่งกลับ (CurrentlyPlayingMatchDto)
            var allPlayersInMatch = await _context.MatchPlayers
                .Where(mp => mp.MatchId == match.MatchId)
                .Include(mp => mp.User.UserProfile)
                .Include(mp => mp.User.SessionParticipants.Where(sp => sp.SessionId == sessionId)).ThenInclude(sp => sp.SkillLevel)
                .Include(mp => mp.Walkin.SkillLevel)
                .ToListAsync();

            Func<MatchPlayer, PlayerInMatchDto> createPlayerDto = p =>
            {
                if (p.UserId.HasValue)
                {
                    var sessionParticipant = p.User?.SessionParticipants.FirstOrDefault();
                    return new PlayerInMatchDto
                    {
                        UserId = p.UserId,
                        Nickname = p.User?.UserProfile?.Nickname ?? "N/A",
                        GenderName = p.User?.UserProfile?.Gender == 1 ? "ชาย" : p.User?.UserProfile?.Gender == 2 ? "หญิง" : "อื่นๆ",
                        SkillLevelId = sessionParticipant?.SkillLevelId,
                        SkillLevelName = sessionParticipant?.SkillLevel?.LevelName,
                        SkillLevelColor = sessionParticipant?.SkillLevel?.ColorHexCode
                    };
                }
                else // WalkinId.HasValue
                {
                    return new PlayerInMatchDto
                    {
                        WalkinId = p.WalkinId,
                        Nickname = p.Walkin?.GuestName ?? "N/A",
                        GenderName = p.Walkin?.Gender == 1 ? "ชาย" : p.Walkin?.Gender == 2 ? "หญิง" : "อื่นๆ",
                        SkillLevelId = p.Walkin?.SkillLevelId,
                        SkillLevelName = p.Walkin?.SkillLevel?.LevelName,
                        SkillLevelColor = p.Walkin?.SkillLevel?.ColorHexCode
                    };
                }
            };

            // 6. Map ข้อมูลไปยัง DTO ที่จะส่งกลับ
            var matchDto = new CurrentlyPlayingMatchDto
            {
                MatchId = match.MatchId,
                CourtNumber = match.CourtNumber,
                StartTime = match.StartTime.Value,
                TeamA = allPlayersInMatch.Where(p => p.Team == "A").Select(createPlayerDto).ToList(),
                TeamB = allPlayersInMatch.Where(p => p.Team == "B").Select(createPlayerDto).ToList()
            };

            // Broadcast state change
            await BroadcastLiveStateChange(sessionId, organizerUserId);

            return matchDto;
        }

        public async Task<bool> EndMatchAsync(int matchId, int organizerUserId)
        {
            var match = await _context.Matches
                .Include(m => m.Session)
                .FirstOrDefaultAsync(m => m.MatchId == matchId);

            if (match == null) return false;

            // ตรวจสอบสิทธิ์ว่าเป็นผู้จัดของก๊วนนี้
            if (match.Session.CreatedByUserId != organizerUserId)
            {
                return false; // ไม่มีสิทธิ์
            }

            match.Status = 2; // 2=จบแล้ว
            match.EndTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Broadcast state change
            await BroadcastLiveStateChange(match.SessionId, organizerUserId);
            return true;
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

        public async Task<BillSummaryDto?> CheckoutParticipantAsync(string participantType, int participantId, int organizerUserId)
        {
            GameSession session = null;
            int? userId = null;
            int? walkinId = null;

            if (participantType == "member")
            {
                var participant = await _context.SessionParticipants
                    .Include(p => p.Session)
                    .FirstOrDefaultAsync(p => p.ParticipantId == participantId);

                if (participant == null || participant.Session.CreatedByUserId != organizerUserId) return null;

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

                guest.CheckoutTime = DateTime.UtcNow;
                session = guest.Session;
                walkinId = guest.WalkinId;
            }
            else
            {
                return null;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var lineItems = new List<BillLineItem>();
                decimal totalAmount = 0;

                if (session.CourtFeePerPerson.HasValue && session.CourtFeePerPerson > 0)
                {
                    var amount = session.CourtFeePerPerson.Value;
                    lineItems.Add(new BillLineItem { Description = "ค่าคอร์ท", Amount = amount });
                    totalAmount += amount;
                }

                if (session.ShuttlecockFeePerPerson.HasValue && session.ShuttlecockFeePerPerson > 0)
                {
                    var amount = session.ShuttlecockFeePerPerson.Value;
                    lineItems.Add(new BillLineItem { Description = "ค่าลูกแบด", Amount = amount });
                    totalAmount += amount;
                }

                var newBill = new ParticipantBill
                {
                    SessionId = session.SessionId,
                    UserId = userId,
                    WalkinId = walkinId,
                    TotalAmount = totalAmount,
                    Status = 1,
                    CreatedDate = DateTime.UtcNow
                };
                await _context.ParticipantBills.AddAsync(newBill);
                await _context.SaveChangesAsync();

                foreach (var item in lineItems)
                {
                    item.BillId = newBill.BillId;
                }
                await _context.BillLineItems.AddRangeAsync(lineItems);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

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
                throw;
            }
        }

        public async Task<(bool Success, string Message)> CheckinParticipantAsync(int sessionId, CheckinDto dto)
        {
            if (dto.ParticipantId.HasValue && !string.IsNullOrEmpty(dto.ParticipantType))
            {
                if (dto.ParticipantType.Equals("Member", StringComparison.OrdinalIgnoreCase))
                {
                    var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.ParticipantId == dto.ParticipantId.Value);
                    if (participant == null) return (false, "Member not found in this session.");
                    if (participant.CheckinTime != null) return (false, "Member already checked in.");

                    participant.CheckinTime = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return (true, "Member checked in successfully.");
                }
                else if (dto.ParticipantType.Equals("Guest", StringComparison.OrdinalIgnoreCase))
                {
                    var guest = await _context.SessionWalkinGuests.FirstOrDefaultAsync(g => g.SessionId == sessionId && g.WalkinId == dto.ParticipantId.Value);
                    if (guest == null) return (false, "Guest not found in this session.");
                    if (guest.CheckinTime != null) return (false, "Guest already checked in.");

                    guest.CheckinTime = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return (true, "Guest checked in successfully.");
                }
                else
                {
                    return (false, "Invalid participant type.");
                }
            }
            else if (!string.IsNullOrEmpty(dto.ScannedData))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserPublicId.ToString() == dto.ScannedData);
                if (user == null) return (false, "User not found from QR code.");

                var participant = await _context.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == user.UserId);
                if (participant == null) return (false, "This user is not registered for this session.");
                if (participant.CheckinTime != null) return (false, "User already checked in.");

                participant.CheckinTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return (true, "Check-in successful.");
            }

            return (false, "Invalid check-in data provided.");
        }

        public async Task<WaitingPlayerDto> AddWalkinGuestAsync(int sessionId, AddWalkinDto dto)
        {
            var newGuest = new SessionWalkinGuest
            {
                SessionId = sessionId,
                GuestName = dto.GuestName,
                Gender = (short?)dto.Gender,
                SkillLevelId = dto.SkillLevelId,
                Status = 1,
                CheckinTime = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow
            };
            await _context.SessionWalkinGuests.AddAsync(newGuest);
            await _context.SaveChangesAsync();

            var skillLevel = newGuest.SkillLevelId.HasValue
                ? await _context.OrganizerSkillLevels.FindAsync(newGuest.SkillLevelId.Value)
                : null;

            return new WaitingPlayerDto
            {
                ParticipantId = newGuest.WalkinId,
                ParticipantType = "Guest",
                Nickname = newGuest.GuestName,
                ProfilePhotoUrl = null,
                SkillLevelName = skillLevel?.LevelName,
                SkillLevelColor = skillLevel?.ColorHexCode,
                CheckedInTime = newGuest.CheckinTime.Value
            };
        }

        public async Task<bool> UpdateParticipantSkillAsync(string participantType, int participantId, UpdateParticipantSkillDto dto)
        {
            if (participantType.Equals("member", StringComparison.OrdinalIgnoreCase))
            {
                var participant = await _context.SessionParticipants.FindAsync(participantId);
                if (participant == null) return false;
                participant.SkillLevelId = dto.SkillLevelId;
            }
            else if (participantType.Equals("guest", StringComparison.OrdinalIgnoreCase))
            {
                var guest = await _context.SessionWalkinGuests.FindAsync(participantId);
                if (guest == null) return false;
                guest.SkillLevelId = dto.SkillLevelId;
            }
            else
            {
                return false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PlayerSessionStatsDto?> GetPlayerSessionStatsAsync(int sessionId, string participantType, int participantId)
        {
            int? targetUserId = null;
            int? targetWalkinId = null;
            string nickname;

            if (participantType.Equals("member", StringComparison.OrdinalIgnoreCase))
            {
                var participant = await _context.SessionParticipants
                    .Include(p => p.User.UserProfile)
                    .FirstOrDefaultAsync(p => p.ParticipantId == participantId && p.SessionId == sessionId);
                if (participant == null) return null;
                targetUserId = participant.UserId;
                nickname = participant.User.UserProfile.Nickname;
            }
            else if (participantType.Equals("guest", StringComparison.OrdinalIgnoreCase))
            {
                var guest = await _context.SessionWalkinGuests
                    .FirstOrDefaultAsync(g => g.WalkinId == participantId && g.SessionId == sessionId);
                if (guest == null) return null;
                targetWalkinId = guest.WalkinId;
                nickname = guest.GuestName;
            }
            else
            {
                return null;
            }

            var playedMatches = await _context.Matches
                .Where(m => m.SessionId == sessionId &&
                            m.MatchPlayers.Any(mp => (targetUserId.HasValue && mp.UserId == targetUserId) ||
                                                     (targetWalkinId.HasValue && mp.WalkinId == targetWalkinId)
                                               ))
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin)
                .OrderByDescending(m => m.StartTime)
                .ToListAsync();

            var stats = new PlayerSessionStatsDto
            {
                ParticipantId = participantId,
                ParticipantType = participantType,
                Nickname = nickname,
                MatchHistory = new List<PlayerMatchHistoryDto>()
            };

            int totalMinutesPlayed = 0;
            foreach (var match in playedMatches)
            {
                var targetPlayerInMatch = match.MatchPlayers
                    .FirstOrDefault(mp => (targetUserId.HasValue && mp.UserId == targetUserId) ||
                                           (targetWalkinId.HasValue && mp.WalkinId == targetWalkinId));

                var teammate = match.MatchPlayers
                    .FirstOrDefault(mp => mp.Team == targetPlayerInMatch.Team && mp.MatchPlayerId != targetPlayerInMatch.MatchPlayerId);

                var opponents = match.MatchPlayers
                    .Where(mp => mp.Team != targetPlayerInMatch.Team && mp.MatchPlayerId != targetPlayerInMatch.MatchPlayerId)
                    .ToList();

                var historyItem = new PlayerMatchHistoryDto
                {
                    MatchId = match.MatchId,
                    CourtNumber = match.CourtNumber,
                    StartTime = match.StartTime ?? DateTime.MinValue,
                    EndTime = match.EndTime ?? DateTime.UtcNow,
                    Teammate = teammate != null ? new PlayerInMatchDto
                    {
                        UserId = teammate.UserId,
                        WalkinId = teammate.WalkinId,
                        Nickname = teammate.UserId.HasValue ? teammate.User?.UserProfile?.Nickname ?? "N/A" : teammate.Walkin?.GuestName ?? "N/A"
                    } : new PlayerInMatchDto { Nickname = "N/A" },
                    Opponents = opponents.Select(o => new PlayerInMatchDto
                    {
                        UserId = o.UserId,
                        WalkinId = o.WalkinId,
                        Nickname = o.UserId.HasValue ? o.User?.UserProfile?.Nickname ?? "N/A" : o.Walkin?.GuestName ?? "N/A"
                    }).ToList()
                };

                if (match.Status == 2 && match.EndTime.HasValue && match.StartTime.HasValue)
                {
                    historyItem.DurationMinutes = (int)(match.EndTime.Value - match.StartTime.Value).TotalMinutes;
                    totalMinutesPlayed += historyItem.DurationMinutes;

                    historyItem.Result = targetPlayerInMatch.Result switch
                    {
                        1 => "Win",
                        2 => "Loss",
                        3 => "Draw",
                        _ => "N/A"
                    };

                    if (targetPlayerInMatch.Result == 1) stats.Wins++;
                    if (targetPlayerInMatch.Result == 2) stats.Losses++;
                }

                stats.MatchHistory.Add(historyItem);
            }

            stats.TotalGamesPlayed = stats.MatchHistory.Count;
            stats.TotalMinutesPlayed = FormatTotalMinutes(totalMinutesPlayed);
            return stats;
        }

        private string FormatTotalMinutes(int totalMinutes)
        {
            if (totalMinutes <= 0)
            {
                return "0 นาที";
            }

            var timeSpan = TimeSpan.FromMinutes(totalMinutes);
            var hours = (int)timeSpan.TotalHours;
            var minutes = timeSpan.Minutes;

            if (hours > 0 && minutes > 0) return $"{hours} ชม. {minutes} นาที";
            if (hours > 0) return $"{hours} ชม.";
            
            return $"{minutes} นาที";
        }

        public async Task<IEnumerable<SessionRosterPlayerDto>?> GetSessionRosterAsync(int sessionId, int organizerUserId)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId)
            {
                return null;
            }

            var members = await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId)
                .Include(p => p.User.UserProfile)
                .Include(p => p.SkillLevel)
                .OrderBy(p => p.CheckinTime)
                .Select(p => new SessionRosterPlayerDto
                {
                    ParticipantId = p.ParticipantId,
                    ParticipantType = "Member",
                    Nickname = p.User.UserProfile.Nickname,
                    FullName = $"{p.User.UserProfile.FirstName} {p.User.UserProfile.LastName}".Trim(),
                    Gender = p.User.UserProfile.Gender == 1 ? "ชาย" :
                             p.User.UserProfile.Gender == 2 ? "หญิง" :
                             p.User.UserProfile.Gender == 3 ? "อื่นๆ" : "ไม่ระบุ",
                    SkillLevelId = p.SkillLevelId,
                    SkillLevelName = p.SkillLevel.LevelName,
                    SkillLevelColor = p.SkillLevel.ColorHexCode,
                    IsCheckedIn = p.CheckinTime != null
                })
                .ToListAsync();

            var guests = await _context.SessionWalkinGuests
                .Where(g => g.SessionId == sessionId)
                .Include(g => g.SkillLevel)
                .OrderBy(g => g.CreatedDate)
                .Select(g => new SessionRosterPlayerDto
                {
                    ParticipantId = g.WalkinId,
                    ParticipantType = "Guest",
                    Nickname = g.GuestName,
                    FullName = g.GuestName,
                    Gender = g.Gender == 1 ? "ชาย" :
                             g.Gender == 2 ? "หญิง" :
                             g.Gender == 3 ? "อื่นๆ" : "ไม่ระบุ",
                    SkillLevelId = g.SkillLevelId,
                    SkillLevelName = g.SkillLevel.LevelName,
                    SkillLevelColor = g.SkillLevel.ColorHexCode,
                    IsCheckedIn = g.CheckinTime != null
                })
                .ToListAsync();

            var allPlayers = members.Cast<object>().Concat(guests.Cast<object>()).ToList();

            var roster = allPlayers.Select((player, index) =>
            {
                var rosterPlayer = (SessionRosterPlayerDto)player;
                rosterPlayer.No = index + 1;
                return rosterPlayer;
            }).ToList();


            return roster;
        }

        public async Task<bool> UpdateSessionCourtsAsync(int sessionId, int organizerUserId, UpdateCourtsDto dto)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId)
            {
                return false;
            }

            session.CourtNumbers = string.Join(",", dto.CourtIdentifiers);
            session.NumberOfCourts = dto.CourtIdentifiers.Count;
            await _context.SaveChangesAsync();

            // Broadcast state change
            await BroadcastLiveStateChange(sessionId, organizerUserId);

            return true;
        }

        public async Task<StagedMatchDto?> CreateStagedMatchAsync(int sessionId, int organizerUserId, CreateStagedMatchDto dto)
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.CreatedByUserId != organizerUserId) return null;

            // กำหนด Target Court ID: ถ้าส่งมาเป็น Null ให้ถือว่าเป็น -1 (String)
            string targetCourtId = dto.courtIdentifier ?? "-1";

            // 1. ค้นหา Match ตาม courtIdentifier ที่ระบุ
            var match = await _context.Matches
                .Include(m => m.MatchPlayers)
                .FirstOrDefaultAsync(m => m.SessionId == sessionId &&
                                            m.Status == 4 && // Staged
                                            m.CourtNumber == targetCourtId);

            // 2. ถ้ายังไม่มี Match ให้สร้างใหม่
            if (match == null)
            {
                match = new Match
                {
                    SessionId = sessionId,
                    CourtNumber = targetCourtId,
                    Status = 4, // Staged
                    CreatedBy = organizerUserId,
                    CreatedDate = DateTime.UtcNow
                };
                await _context.Matches.AddAsync(match);
                await _context.SaveChangesAsync();
            }
            else
            {
                // *** Full Sync Logic: ล้างผู้เล่นเดิมออก เพื่อบันทึกชุดใหม่ที่ส่งมา ***
                if (match.MatchPlayers != null && match.MatchPlayers.Any())
                {
                    _context.MatchPlayers.RemoveRange(match.MatchPlayers);
                    await _context.SaveChangesAsync(); // บันทึกการล้างผู้เล่นเก่าทันที
                }
            }

            // 3. เตรียมข้อมูลผู้เล่นใหม่ (New Players)
            var validTeamA = dto.TeamA?.Where(p => p != null).ToList() ?? new List<PlayerSelectionDto>();
            var validTeamB = dto.TeamB?.Where(p => p != null).ToList() ?? new List<PlayerSelectionDto>();

            // ถ้าไม่มีผู้เล่นส่งมาเลย และมี match อยู่ ให้ลบ match นั้นทิ้ง
            if (!validTeamA.Any() && !validTeamB.Any())
            {
                if (match != null)
                {
                    if (match.MatchPlayers != null && match.MatchPlayers.Any())
                    {
                        _context.MatchPlayers.RemoveRange(match.MatchPlayers);
                    }
                    _context.Matches.Remove(match);
                    await _context.SaveChangesAsync();
                }
                return null; // ไม่มีผู้เล่น ไม่ต้องสร้าง DTO
            }

            // // ตรวจสอบว่าผู้เล่นที่ส่งมาไม่ได้อยู่ใน Staged Match อื่น (ทั้งในสนามจริงและทีมสำรอง)
            // var allPlayersInDto = validTeamA.Concat(validTeamB).ToList();
            // var memberParticipantIds = allPlayersInDto.Where(p => p.Type == "Member").Select(p => p.Id).ToList();
            // var guestWalkinIds = allPlayersInDto.Where(p => p.Type == "Guest").Select(p => p.Id).ToList();
            //
            // var memberUserIdsInDto = await _context.SessionParticipants
            //     .Where(sp => memberParticipantIds.Contains(sp.ParticipantId))
            //     .Select(sp => sp.UserId)
            //     .ToListAsync();
            //
            // // ดึงผู้เล่นทั้งหมดที่อยู่ใน Staged Match อื่นๆ (ที่ไม่ใช่ targetCourtId)
            // var playersInOtherStagedMatches = await _context.MatchPlayers
            //     .Where(mp => mp.Match.SessionId == sessionId &&
            //                  mp.Match.Status == 4 && // Staged
            //                  mp.Match.CourtNumber != targetCourtId) // Match อื่นๆ
            //     .Select(mp => new { mp.UserId, mp.WalkinId })
            //     .ToListAsync();
            //
            // var otherStagedUserIds = playersInOtherStagedMatches.Where(p => p.UserId.HasValue).Select(p => p.UserId.Value).ToHashSet();
            // var otherStagedWalkinIds = playersInOtherStagedMatches.Where(p => p.WalkinId.HasValue).Select(p => p.WalkinId.Value).ToHashSet();
            //
            // var isPlayerInAnotherStagedMatch = memberUserIdsInDto.Any(id => otherStagedUserIds.Contains(id)) ||
            //                                    guestWalkinIds.Any(id => otherStagedWalkinIds.Contains(id));
            //
            // if (isPlayerInAnotherStagedMatch) return null; // ผู้เล่นอยู่ในสนามอื่นแล้ว

            var allMemberIds = validTeamA.Concat(validTeamB)
                .Where(p => p.Type == "Member")
                .Select(p => p.Id)
                .Distinct()
                .ToList();

            var memberUserIdMap = await _context.SessionParticipants
                .Where(sp => allMemberIds.Contains(sp.ParticipantId))
                .ToDictionaryAsync(sp => sp.ParticipantId, sp => sp.UserId);

            var newMatchPlayers = new List<MatchPlayer>();

            void PreparePlayers(List<PlayerSelectionDto> players, string team)
            {
                foreach (var p in players)
                {
                    if (p == null) continue;

                    int? userId = null;
                    int? walkinId = null;

                    if (p.Type == "Member" && memberUserIdMap.ContainsKey(p.Id))
                    {
                        userId = memberUserIdMap[p.Id];
                    }
                    else if (p.Type == "Guest")
                    {
                        walkinId = p.Id;
                    }

                    if (userId.HasValue || walkinId.HasValue)
                    {
                        newMatchPlayers.Add(new MatchPlayer
                        {
                            MatchId = match.MatchId,
                            Team = team,
                            UserId = userId,
                            WalkinId = walkinId
                        });
                    }
                }
            }

            PreparePlayers(validTeamA, "A");
            PreparePlayers(validTeamB, "B");

            if (newMatchPlayers.Any())
            {
                await _context.MatchPlayers.AddRangeAsync(newMatchPlayers);
            }

            await _context.SaveChangesAsync();

            // 5. Return DTO
            var createdMatchPlayers = await _context.MatchPlayers
                .Where(mp => mp.MatchId == match.MatchId)
                .Include(mp => mp.User.UserProfile)
                .Include(mp => mp.Walkin)
                .ToListAsync();

            var stagedMatchDto = new StagedMatchDto
            {
                MatchId = match.MatchId,
                CourtNumber = match.CourtNumber,
                TeamA = createdMatchPlayers.Where(p => p.Team == "A").Select(p => new PlayerInMatchDto
                {
                    UserId = p.UserId,
                    WalkinId = p.WalkinId,
                    Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                    ProfilePhotoUrl = p.UserId.HasValue ? p.User?.UserProfile?.ProfilePhotoUrl : null
                }).ToList(),
                TeamB = createdMatchPlayers.Where(p => p.Team == "B").Select(p => new PlayerInMatchDto
                {
                    UserId = p.UserId,
                    WalkinId = p.WalkinId,
                    Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                    ProfilePhotoUrl = p.UserId.HasValue ? p.User?.UserProfile?.ProfilePhotoUrl : null
                }).ToList()
            };

            // Broadcast state change
            await BroadcastLiveStateChange(sessionId, organizerUserId);

            return stagedMatchDto;
        }

        public async Task<CurrentlyPlayingMatchDto?> StartStagedMatchAsync(int matchId, int organizerUserId, StartStagedMatchDto dto)
        {
            var match = await _context.Matches
                .Include(m => m.Session)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.User.UserProfile)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Walkin)
                .FirstOrDefaultAsync(m => m.MatchId == matchId);

            if (match == null || match.Session.CreatedByUserId != organizerUserId || match.Status != 4)
            {
                return null;
            }

            // ใช้ CourtNumber ที่อยู่ใน Match (ถ้ามี) หรือจาก DTO
            var courtNumberToAssign = match.CourtNumber ?? dto.CourtNumber;

            // ตรวจสอบว่าสนามที่จะเริ่มแข่งว่างอยู่หรือไม่ (เช็คแบบ String)
            if (!string.IsNullOrEmpty(courtNumberToAssign))
            {
                var isCourtOccupied = await _context.Matches.AnyAsync(m => m.SessionId == match.SessionId && m.Status == 1 && m.CourtNumber == courtNumberToAssign);
                if (isCourtOccupied)
                {
                    return null; // สนามไม่ว่าง
                }
            }
            else
            {
                // ถ้าไม่มีการระบุ CourtNumber มาเลย ก็ไม่สามารถเริ่มได้
                return null;
            }

            match.Status = 1; // 1=Playing
            match.CourtNumber = courtNumberToAssign;
            match.StartTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var matchDto = new CurrentlyPlayingMatchDto
            {
                MatchId = match.MatchId,
                CourtNumber = match.CourtNumber,
                StartTime = match.StartTime.Value,
                TeamA = match.MatchPlayers.Where(p => p.Team == "A").Select(p => new PlayerInMatchDto { UserId = p.UserId, WalkinId = p.WalkinId, Nickname = p.UserId.HasValue ? p.User.UserProfile.Nickname : p.Walkin.GuestName }).ToList(),
                TeamB = match.MatchPlayers.Where(p => p.Team == "B").Select(p => new PlayerInMatchDto { UserId = p.UserId, WalkinId = p.WalkinId, Nickname = p.UserId.HasValue ? p.User.UserProfile.Nickname : p.Walkin.GuestName }).ToList()
            };

            // Broadcast state change
            await BroadcastLiveStateChange(match.SessionId, organizerUserId);

            return matchDto;
        }

        private async Task BroadcastLiveStateChange(int sessionId, int organizerUserId)
        {
            // ใช้ IServiceProvider เพื่อ resolve service และหลีกเลี่ยง circular dependency
            using (var scope = _serviceProvider.CreateScope())
            {
                var matchService = scope.ServiceProvider.GetRequiredService<IMatchManagementService>();
                var liveState = await matchService.GetLiveStateAsync(sessionId, organizerUserId);
                if (liveState != null)
                {
                    await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("ReceiveLiveStateUpdate", liveState);
                }
            }
        }
    }
}