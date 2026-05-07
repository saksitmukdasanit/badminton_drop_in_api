using Microsoft.Extensions.Configuration;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Hubs;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Services
{
    public partial class MatchManagementService
    {
        public async Task<OrganizerFinanceDashboardDto> GetFinanceDashboardAsync(int organizerUserId)
        {
            var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == organizerUserId);
            decimal balance = wallet?.Balance ?? 0;

            var profile = await _context.OrganizerProfiles.Include(p => p.Bank).FirstOrDefaultAsync(p => p.UserId == organizerUserId);

            var sessions = await _context.GameSessions
                .Where(s => s.CreatedByUserId == organizerUserId && s.Status != 3) // ไม่รวมก๊วนยกเลิก
                .Include(s => s.ParticipantBills)
                .Include(s => s.SessionParticipants)
                .ToListAsync();

            decimal totalIncome = sessions.SelectMany(s => s.ParticipantBills).Where(b => b.Status == 2).Sum(b => b.TotalAmount);
            decimal pendingAmount = sessions.SelectMany(s => s.ParticipantBills).Where(b => b.Status == 1).Sum(b => b.TotalAmount);

            var latestSessions = sessions.OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.StartTime).Take(5).ToList();
            var chartData = latestSessions.Select(s => new FinanceChartGameDto
            {
                Name = s.GroupName,
                PlayersCount = s.SessionParticipants.Count(p => p.Status == 1),
                PaidCount = s.ParticipantBills.Count(b => b.SessionId == s.SessionId && b.Status == 2)
            }).Reverse().ToList();

            decimal chartTotalIncome = latestSessions.SelectMany(s => s.ParticipantBills).Where(b => b.Status == 2).Sum(b => b.TotalAmount);

            return new OrganizerFinanceDashboardDto
            {
                Balance = balance,
                TotalIncome = totalIncome,
                PendingAmount = pendingAmount,
                ChartTotalIncome = chartTotalIncome,
                LatestGames = chartData,
                BankName = profile?.Bank?.BankName,
                BankAccountNumber = profile?.BankAccountNumber,
                BankAccountPhotoUrl = profile?.BankAccountPhotoUrl,
                NationalId = profile?.NationalId
            };
        }

        public async Task<(bool Success, string Message)> WithdrawOrganizerFundsAsync(int organizerUserId, decimal amount)
        {
            if (amount <= 0) return (false, "จำนวนเงินต้องมากกว่า 0 บาท");

            var profile = await _context.OrganizerProfiles.Include(p => p.Bank).Include(p => p.User).ThenInclude(u => u.UserProfile).FirstOrDefaultAsync(p => p.UserId == organizerUserId);
            // BankId เป็น int (non-null) — ถ้ายังไม่เลือกธนาคารจะเป็น 0
            if (profile == null || profile.BankId <= 0 || profile.Bank == null || string.IsNullOrEmpty(profile.BankAccountNumber))
            {
                return (false, "กรุณาตั้งค่าบัญชีรับเงินของผู้จัดในหน้าโปรไฟล์ให้เรียบร้อยก่อนทำรายการ");
            }

            var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == organizerUserId);
            if (wallet == null || wallet.Balance < amount) return (false, "ยอดเงินคงเหลือในระบบไม่เพียงพอ");

            // --- NEW: ยิง API สั่งให้ Xendit โอนเงินเข้าบัญชี (Payout) ---
            string bankCode = profile.Bank?.BankCode ?? "";
            if (string.IsNullOrEmpty(bankCode)) return (false, "ไม่พบรหัสธนาคารในระบบ ไม่สามารถทำรายการโอนได้");

            string accountName = profile.User?.UserProfile?.FirstName + " " + profile.User?.UserProfile?.LastName;
            if (string.IsNullOrWhiteSpace(accountName)) accountName = profile.User?.UserProfile?.Nickname ?? "DropInBad User";

            string refId = $"ORG-{organizerUserId}-{DateTime.UtcNow.Ticks}";
            var (payoutSuccess, payoutMessage, payoutId) = await _xenditService.CreatePayoutAsync(
                refId, amount, bankCode, accountName, profile.BankAccountNumber, $"ถอนเงินรายได้ผู้จัด: {profile.User?.UserProfile?.Nickname}", profile.XenditAccountId
            );

            if (!payoutSuccess) return (false, $"การโอนเงินขัดข้อง: {payoutMessage}");

            wallet.Balance -= amount;
            wallet.UpdatedDate = DateTime.UtcNow;

            var transaction = new WalletTransaction
            {
                Wallet = wallet,
                Amount = amount,
                TransactionType = 2, // 2 = OUT (Withdraw)
                Description = $"ถอนเงินรายได้ผู้จัดเข้าบัญชี {profile.Bank?.BankName} ({profile.BankAccountNumber}) [Ref: {payoutId}]",
                CreatedDate = DateTime.UtcNow
            };

            await _context.WalletTransactions.AddAsync(transaction);
            await _context.SaveChangesAsync();

            return (true, "ทำรายการถอนเงินและโอนเงินเข้าบัญชีสำเร็จ ระบบจะส่งยอดเข้าบัญชีของคุณเร็วๆ นี้");
        }

    }
}
