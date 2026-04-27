using System.Linq;
using System.Threading.Tasks;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Services
{
    public class WalletService : IWalletService
    {
        private readonly BadmintonDbContext _context;
        private readonly IXenditService _xenditService;

        public WalletService(BadmintonDbContext context, IXenditService xenditService)
        {
            _context = context;
            _xenditService = xenditService;
        }

        public async Task<WalletDto> GetMyWalletAsync(int userId)
        {
            var wallet = await _context.UserWallets
                .Include(w => w.WalletTransactions)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null) return new WalletDto { Balance = 0, Transactions = new() };

            var transactions = wallet.WalletTransactions
                .OrderByDescending(t => t.CreatedDate)
                .Select(t => new WalletTransactionDto
                {
                    TransactionId = t.TransactionId, Amount = t.Amount,
                    TransactionType = t.TransactionType, Description = t.Description, CreatedDate = t.CreatedDate
                }).ToList();

            return new WalletDto { Balance = wallet.Balance, Transactions = transactions };
        }

        public async Task<(bool Success, string Message)> WithdrawAsync(int userId, decimal amount)
        {
            if (amount <= 0) return (false, "จำนวนเงินต้องมากกว่า 0 บาท");

            // 1. ตรวจสอบว่าผู้เล่นมีการตั้งค่าบัญชีธนาคารไว้หรือยัง
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null || profile.BankId == null || string.IsNullOrEmpty(profile.BankAccountNumber))
            {
                return (false, "กรุณาตั้งค่าบัญชีธนาคารในเมนู 'บัญชีรับเงิน' ให้เรียบร้อยก่อนทำการถอนเงิน");
            }

            // 2. ตรวจสอบยอดเงินในกระเป๋า
            var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null || wallet.Balance < amount)
            {
                return (false, "ยอดเงินในกระเป๋าไม่เพียงพอ");
            }

            string bankName = "ธนาคาร";
            string bankCode = "";
            var bank = await _context.Banks.FindAsync(profile.BankId);
            if (bank != null)
            {
                bankName = bank.BankName;
                bankCode = bank.BankCode ?? "";
            }

            if (string.IsNullOrEmpty(bankCode)) return (false, "ข้อมูลธนาคารไม่สมบูรณ์ (ไม่พบ Bank Code) ไม่สามารถโอนเงินได้");

            // --- NEW: ยิง API สั่งให้ Xendit โอนเงินเข้าบัญชี (Payout) ---
            string accountName = profile.BankAccountName;
            if (string.IsNullOrWhiteSpace(accountName)) accountName = $"{profile.FirstName} {profile.LastName}";
            if (string.IsNullOrWhiteSpace(accountName)) accountName = profile.Nickname ?? "DropInBad Player";

            string refId = $"PLY-{userId}-{DateTime.UtcNow.Ticks}";
            var (payoutSuccess, payoutMessage, payoutId) = await _xenditService.CreatePayoutAsync(
                refId, amount, bankCode, accountName, profile.BankAccountNumber, $"ถอนเงิน Wallet: {profile.Nickname}"
            );

            if (!payoutSuccess) return (false, $"การโอนเงินขัดข้อง: {payoutMessage}");

            // 3. หักยอดเงินและบันทึกประวัติ (หักหลังจากที่ Xendit รับคำสั่งสำเร็จเท่านั้น)
            wallet.Balance -= amount;
            wallet.UpdatedDate = DateTime.UtcNow;

            var transaction = new WalletTransaction
            {
                Wallet = wallet,
                Amount = amount,
                TransactionType = 2, // 2 = OUT (Withdraw)
                Description = $"ถอนเงินเข้าบัญชี {bankName} ({profile.BankAccountNumber}) [Ref: {payoutId}]",
                CreatedDate = DateTime.UtcNow
            };

            await _context.WalletTransactions.AddAsync(transaction);
            await _context.SaveChangesAsync();

            return (true, "ทำรายการถอนเงินสำเร็จ ระบบกำลังดำเนินการโอนยอดเข้าบัญชีของคุณ");
        }
    }
}