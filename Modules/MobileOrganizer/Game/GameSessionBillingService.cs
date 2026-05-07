using DropInBadAPI.Constants;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DropInBadAPI.Service.Mobile.Game;

public class GameSessionBillingService : IGameSessionBillingService
{
    private readonly BadmintonDbContext _context;
    private readonly IConfiguration _configuration;

    public GameSessionBillingService(BadmintonDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<GameSessionFinancialsDto?> GetSessionFinancialsAsync(int sessionId, int organizerUserId)
    {
        var session = await _context.GameSessions
            .Include(s => s.SessionParticipants).ThenInclude(p => p.User!).ThenInclude(u => u.UserProfile)
            .Include(s => s.SessionWalkinGuests)
            .Include(s => s.ParticipantBills).ThenInclude(b => b.BillLineItems)
            .Include(s => s.Matches).ThenInclude(m => m.MatchPlayers)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.CreatedByUserId == organizerUserId);

        if (session == null) return null;

        var activeMembers = session.SessionParticipants.Where(p => p.Status == 1).ToList();
        var activeGuests = session.SessionWalkinGuests.Where(g => g.Status == 1).ToList();
        int currentParticipants = activeMembers.Count + activeGuests.Count;

        decimal courtFeePerPerson = session.CourtFeePerPerson ?? 0;
        decimal shuttleFeePerPerson = session.ShuttlecockFeePerPerson ?? 0;
        decimal totalCourtCost = session.TotalCourtCost ?? 0;
        decimal shuttleCostPerUnit = session.ShuttlecockCostPerUnit ?? 0;

        int CountGames(int? userId, int? walkinId)
        {
            return session.Matches.Count(m => m.Status == 2 && m.MatchPlayers.Any(mp => mp.UserId == userId && mp.WalkinId == walkinId));
        }

        decimal aggTotalCourtIncome = 0;
        decimal aggTotalShuttleFee = 0;
        decimal aggTotalIncome = 0;
        decimal aggPaidAmount = 0;
        decimal aggUnpaidAmount = 0;

        decimal aggNetTotalIncome = 0;
        decimal aggNetPaidAmount = 0;
        decimal aggNetUnpaidAmount = 0;
        decimal aggTotalServiceFee = 0;

        int countPaidCourt = 0;
        int countUnpaidCourt = 0;
        decimal sumPaidCourt = 0;
        decimal sumUnpaidCourt = 0;
        decimal sumPaidShuttle = 0;
        decimal sumUnpaidShuttle = 0;
        decimal sumAdditions = 0;
        decimal sumSubtractions = 0;

        decimal serviceFee = _configuration.GetValue<decimal>("ServiceFee");

        var participantDtos = new List<ParticipantFinancialDto>();

        (decimal paid, decimal total, decimal courtPart, decimal shuttlePart, decimal srvFee, decimal netTotal, decimal netPaid, decimal netUnpaid) CalculateParticipantFinancials(int? userId, int? walkinId, int gamesPlayed)
        {
            var bills = session.ParticipantBills.Where(b => b.UserId == userId && b.WalkinId == walkinId && b.Status != 3).ToList();

            var activeBills = bills.Where(b => b.Status == 2).ToList();
            if (!activeBills.Any())
            {
                var latestPending = bills.Where(b => b.Status == 1).OrderByDescending(b => b.CreatedDate).FirstOrDefault();
                if (latestPending != null) activeBills.Add(latestPending);
            }

            decimal cPart = courtFeePerPerson;
            decimal sPart = session.CostingMethod == 2 ? shuttleFeePerPerson : shuttleFeePerPerson * gamesPlayed;
            decimal customItems = 0;

            if (activeBills.Any())
            {
                cPart = activeBills.SelectMany(b => b.BillLineItems).Where(li => li.Description == "ค่าสนาม" || li.Description == "ค่าคอร์ท").Sum(li => li.Amount);
                if (cPart == 0 && courtFeePerPerson > 0) cPart = courtFeePerPerson;

                sPart = activeBills.SelectMany(b => b.BillLineItems).Where(li => li.Description.StartsWith("ค่าลูกแบด")).Sum(li => li.Amount);
                if (sPart == 0) sPart = session.CostingMethod == 2 ? shuttleFeePerPerson : shuttleFeePerPerson * gamesPlayed;

                customItems = activeBills.SelectMany(b => b.BillLineItems).Where(li => li.Description != "ค่าสนาม" && li.Description != "ค่าคอร์ท" && li.Description != "ค่าธรรมเนียม" && !li.Description.StartsWith("ค่าลูกแบด")).Sum(li => li.Amount);
            }

            decimal serviceFeeTotal = activeBills.SelectMany(b => b.BillLineItems).Where(li => li.Description == "ค่าธรรมเนียม").Sum(li => li.Amount);
            decimal serviceFeePaid = bills.Where(b => b.Status == 2).SelectMany(b => b.BillLineItems).Where(li => li.Description == "ค่าธรรมเนียม").Sum(li => li.Amount);

            decimal paidVal = bills.Where(b => b.Status == 2).Sum(b => b.TotalAmount) - serviceFeePaid;
            if (paidVal < 0) paidVal = 0;

            decimal billedTotal = activeBills.Sum(b => b.TotalAmount) - serviceFeeTotal;
            if (billedTotal < 0) billedTotal = 0;

            decimal totalVal = cPart + sPart + customItems;
            if (billedTotal > totalVal) totalVal = billedTotal;

            decimal unpaidVal = totalVal - paidVal;
            if (unpaidVal < 0) unpaidVal = 0;

            decimal actualServiceFee = serviceFeeTotal > 0 ? serviceFeeTotal : (activeBills.Any() ? 0 : serviceFee);

            return (paidVal, totalVal, cPart, sPart, actualServiceFee, totalVal, paidVal, unpaidVal);
        }

        void CalculateBreakdown(decimal totalCost, decimal paidAmount, decimal courtFee, decimal shuttleFee)
        {
            decimal ratio = totalCost > 0 ? paidAmount / totalCost : 0;
            if (ratio > 1) ratio = 1;

            decimal cPaid = courtFee * ratio;
            decimal cUnpaid = courtFee - cPaid;
            sumPaidCourt += cPaid;
            sumUnpaidCourt += cUnpaid;
            if (cUnpaid <= 1) countPaidCourt++; else countUnpaidCourt++;

            decimal sPaid = shuttleFee * ratio;
            decimal sUnpaid = shuttleFee - sPaid;
            sumPaidShuttle += sPaid;
            sumUnpaidShuttle += sUnpaid;

            decimal standardTotal = courtFee + shuttleFee + serviceFee;
            decimal diff = totalCost - standardTotal;
            if (diff > 0.1m) sumAdditions += diff;
            else if (diff < -0.1m) sumSubtractions += diff;
        }

        foreach (var m in activeMembers)
        {
            int games = CountGames(m.UserId, null);
            var (paid, total, cPart, sPart, srvFee, netTotal, netPaid, netUnpaid) = CalculateParticipantFinancials(m.UserId, null, games);

            aggTotalCourtIncome += cPart;
            aggTotalShuttleFee += sPart;
            aggTotalIncome += total;
            aggPaidAmount += paid;
            aggUnpaidAmount += (total - paid > 0 ? total - paid : 0);

            aggNetTotalIncome += netTotal;
            aggNetPaidAmount += netPaid;
            aggNetUnpaidAmount += netUnpaid;
            aggTotalServiceFee += srvFee;

            CalculateBreakdown(total, paid, cPart, sPart);

            participantDtos.Add(new ParticipantFinancialDto
            {
                ParticipantId = m.ParticipantId,
                ParticipantType = ParticipantTypes.Member,
                Nickname = m.User?.UserProfile?.Nickname ?? "N/A",
                Name = $"{m.User?.UserProfile?.FirstName} {m.User?.UserProfile?.LastName}",
                GamesPlayed = games,
                TotalCost = total,
                PaidAmount = paid,
                UnpaidAmount = total - paid > 0 ? total - paid : 0,
                CourtFee = cPart,
                ShuttleFee = sPart,
                ServiceFee = srvFee,
                OrganizerNetTotal = netTotal,
                OrganizerNetPaid = netPaid,
                OrganizerNetUnpaid = netUnpaid
            });
        }

        foreach (var g in activeGuests)
        {
            int games = CountGames(null, g.WalkinId);
            var (paid, total, cPart, sPart, srvFee, netTotal, netPaid, netUnpaid) = CalculateParticipantFinancials(null, g.WalkinId, games);

            aggTotalCourtIncome += cPart;
            aggTotalShuttleFee += sPart;
            aggTotalIncome += total;
            aggPaidAmount += paid;
            aggUnpaidAmount += (total - paid > 0 ? total - paid : 0);

            aggNetTotalIncome += netTotal;
            aggNetPaidAmount += netPaid;
            aggNetUnpaidAmount += netUnpaid;
            aggTotalServiceFee += srvFee;

            CalculateBreakdown(total, paid, cPart, sPart);

            participantDtos.Add(new ParticipantFinancialDto
            {
                ParticipantId = g.WalkinId,
                ParticipantType = ParticipantTypes.Guest,
                Nickname = g.GuestName,
                Name = g.GuestName,
                GamesPlayed = games,
                TotalCost = total,
                PaidAmount = paid,
                UnpaidAmount = total - paid > 0 ? total - paid : 0,
                CourtFee = cPart,
                ShuttleFee = sPart,
                ServiceFee = srvFee,
                OrganizerNetTotal = netTotal,
                OrganizerNetPaid = netPaid,
                OrganizerNetUnpaid = netUnpaid
            });
        }

        int totalShuttlecocksUsed = session.Matches.Count(m => m.Status == 2);
        decimal totalShuttleCost = totalShuttlecocksUsed * shuttleCostPerUnit;

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
            ShuttlecockCostPerUnit = shuttleCostPerUnit,
            CostingMethod = session.CostingMethod.HasValue ? (int)session.CostingMethod.Value : null,
            TotalCourtCost = totalCourtCost,
            TotalCourtIncome = aggTotalCourtIncome,
            TotalShuttlecockFee = aggTotalShuttleFee,
            TotalShuttlecockCost = totalShuttleCost,
            TotalIncome = aggTotalIncome,
            TotalExpense = totalCourtCost + totalShuttleCost,
            PaidAmount = aggPaidAmount,
            TotalCashAmount = totalCash,
            TotalTransferAmount = totalTransfer,
            UnpaidAmount = aggUnpaidAmount,
            TotalShuttlecocks = totalShuttlecocksUsed,
            Participants = participantDtos,
            PaidCourtCount = countPaidCourt,
            UnpaidCourtCount = countUnpaidCourt,
            PaidCourtAmount = sumPaidCourt,
            UnpaidCourtAmount = sumUnpaidCourt,
            PaidShuttleAmount = sumPaidShuttle,
            UnpaidShuttleAmount = sumUnpaidShuttle,
            TotalAdditions = sumAdditions,
            TotalSubtractions = sumSubtractions,
            TotalServiceFeeDeducted = aggTotalServiceFee,
            OrganizerNetTotalIncome = aggNetTotalIncome,
            OrganizerNetPaidAmount = aggNetPaidAmount,
            OrganizerNetUnpaidAmount = aggNetUnpaidAmount
        };
    }
}
