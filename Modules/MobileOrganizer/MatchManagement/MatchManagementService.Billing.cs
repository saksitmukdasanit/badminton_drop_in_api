using Microsoft.Extensions.Configuration;
using DropInBadAPI.Constants;
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
        // --- NEW: ฟังก์ชันสำหรับดูยอด (เรียกใช้ Logic คำนวณร่วมกัน) ---
        public async Task<BillSummaryDto?> GetParticipantBillPreviewAsync(string participantType, int participantId, int organizerUserId)
        {
            // เรียกใช้ Helper เพื่อคำนวณยอด แต่ไม่บันทึก (isPreview = true)
            return await CalculateAndSaveBillAsync(participantType, participantId, organizerUserId, null, isPreview: true);
        }

        // --- UPDATED: ฟังก์ชันสำหรับเช็คบิลจริง ---
        public async Task<BillSummaryDto?> CheckoutParticipantAsync(string participantType, int participantId, int organizerUserId, CheckoutRequestDto? customCheckout = null)
        {
            // เรียกใช้ Helper เพื่อคำนวณและบันทึก (isPreview = false)
            return await CalculateAndSaveBillAsync(participantType, participantId, organizerUserId, customCheckout, isPreview: false);
        }

        // --- HELPER: รวม Logic การคำนวณไว้ที่นี่ ---
        private async Task<BillSummaryDto?> CalculateAndSaveBillAsync(
            string participantType,
            int participantId,
            int organizerUserId,
            CheckoutRequestDto? customCheckout,
            bool isPreview)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                GameSession? session = null;
                int? userId = null;
                int? walkinId = null;

                // FIX: ใช้ StringComparison.OrdinalIgnoreCase เพื่อความชัวร์
                if (ParticipantTypes.IsMember(participantType))
                {
                    var participant = await _context.SessionParticipants
                        .Include(p => p.Session)
                        .FirstOrDefaultAsync(p => p.ParticipantId == participantId);

                    if (participant == null || participant.Session.CreatedByUserId != organizerUserId) return null;

                    // ถ้าไม่ใช่ Preview ถึงจะอัปเดตเวลาออก
                    if (!isPreview) participant.CheckoutTime = DateTime.UtcNow;
                    session = participant.Session;
                    userId = participant.UserId;
                }
                else if (ParticipantTypes.IsGuest(participantType))
                {
                    var guest = await _context.SessionWalkinGuests
                        .Include(g => g.Session)
                        .FirstOrDefaultAsync(g => g.WalkinId == participantId);

                    if (guest == null || guest.Session.CreatedByUserId != organizerUserId) return null;

                    // ถ้าไม่ใช่ Preview ถึงจะอัปเดตเวลาออก
                    if (!isPreview) guest.CheckoutTime = DateTime.UtcNow;
                    session = guest.Session;
                    walkinId = guest.WalkinId;
                }
                else
                {
                    return null;
                }


                // --- NEW: ดึงข้อมูลแมตช์ที่เล่นจบแล้วเพื่อคำนวณค่าใช้จ่ายตามจริง ---
                var matchesPlayed = await _context.Matches
                    // FIX: นับเกมที่ "กำลังเล่น (1)" และ "จบแล้ว (2)" เพื่อให้คิดเงินได้แม้จะ Checkout ตอนเกมยังไม่จบ
                    .Where(m => m.SessionId == session.SessionId && (m.Status == 2 || m.Status == 1) &&
                                m.MatchPlayers.Any(mp => (userId != null && mp.UserId == userId) || (walkinId != null && mp.WalkinId == walkinId)))
                    .Include(m => m.MatchPlayers)
                    .ToListAsync();

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var lineItems = new List<BillLineItem>();
                    decimal totalAmount = 0;

                    // ดึงประวัติบิลที่ชำระเงินแล้วเพื่อหักลบกลบยอด
                    var pastBills = await _context.ParticipantBills.Include(b => b.BillLineItems)
                        .Where(b => b.SessionId == session.SessionId && b.UserId == userId && b.WalkinId == walkinId && b.Status == 2).ToListAsync();

                    bool courtPaid = pastBills.Any(b => b.BillLineItems.Any(li => li.Description == "ค่าสนาม"));
                    bool servicePaid = pastBills.Any(b => b.BillLineItems.Any(li => li.Description == "ค่าธรรมเนียม"));

                    // --- FIX: คำนวณค่าพื้นฐานฝั่ง Server เสมอ (ไม่เชื่อใจ Frontend 100% เพื่อป้องกันข้อมูลขาดหาย) ---
                    if (!courtPaid && session.CourtFeePerPerson.HasValue && session.CourtFeePerPerson > 0)
                    {
                        lineItems.Add(new BillLineItem { Description = "ค่าสนาม", Amount = session.CourtFeePerPerson.Value });
                        totalAmount += session.CourtFeePerPerson.Value;
                    }

                    if (!servicePaid)
                    {
                        decimal serviceFee = _configuration.GetValue<decimal>("ServiceFee");
                        if (serviceFee > 0)
                        {
                            lineItems.Add(new BillLineItem { Description = "ค่าธรรมเนียม", Amount = serviceFee });
                            totalAmount += serviceFee;
                        }
                    }

                    decimal shuttleTotal = 0;
                    bool isBuffet = session.CostingMethod == 2;

                    if (isBuffet && session.ShuttlecockFeePerPerson.HasValue && session.ShuttlecockFeePerPerson > 0)
                    {
                        shuttleTotal = session.ShuttlecockFeePerPerson.Value;
                    }
                    else if (session.ShuttlecockFeePerPerson.HasValue && session.ShuttlecockFeePerPerson > 0)
                    {
                        shuttleTotal = session.ShuttlecockFeePerPerson.Value * matchesPlayed.Count;
                    }

                    decimal paidShuttle = pastBills.SelectMany(b => b.BillLineItems).Where(li => li.Description.StartsWith("ค่าลูกแบด")).Sum(li => li.Amount);
                    decimal dueShuttle = shuttleTotal - paidShuttle;
                    if (dueShuttle > 0)
                    {
                        lineItems.Add(new BillLineItem { Description = isBuffet ? "ค่าลูกแบด (เหมาจ่าย)" : $"ค่าลูกแบด ({matchesPlayed.Count} เกม)", Amount = dueShuttle });
                        totalAmount += dueShuttle;
                    }

                    // --- FIX: รับรายการปรับปรุง (Adjustments) จาก Frontend มาเพิ่ม/ลดยอดเท่านั้น ---
                    if (customCheckout != null && customCheckout.CustomLineItems != null && customCheckout.CustomLineItems.Any())
                    {
                        foreach (var item in customCheckout.CustomLineItems)
                        {
                            if (item.Description == "ค่าสนาม" || item.Description == "ค่าคอร์ท" || item.Description == "ค่าธรรมเนียม" || item.Description.StartsWith("ค่าลูกแบด")) continue;
                            lineItems.Add(new BillLineItem { Description = item.Description, Amount = item.Amount });
                            totalAmount += item.Amount;
                        }
                    }
                    else if (!isPreview)
                    {
                        var pendingBills = await _context.ParticipantBills.Include(b => b.BillLineItems).Where(b => b.SessionId == session.SessionId && b.UserId == userId && b.WalkinId == walkinId && b.Status == 1).ToListAsync();
                        if (pendingBills.Any())
                        {
                            var latestPending = pendingBills.OrderByDescending(b => b.CreatedDate).First();
                            var customItems = latestPending.BillLineItems.Where(li => li.Description != "ค่าสนาม" && li.Description != "ค่าธรรมเนียม" && !li.Description.StartsWith("ค่าลูกแบด"));
                            foreach (var item in customItems)
                            {
                                lineItems.Add(new BillLineItem { Description = item.Description, Amount = item.Amount });
                                totalAmount += item.Amount;
                            }
                        }
                    }

                    // --- NEW: ยกเลิกบิลค้างชำระเดิมทั้งหมดเสมอ เพื่อป้องกันยอดซ้ำซ้อน ---
                    if (!isPreview)
                    {
                        var allPendingBills = await _context.ParticipantBills.Where(b => b.SessionId == session.SessionId && b.UserId == userId && b.WalkinId == walkinId && b.Status == 1).ToListAsync();
                        foreach (var pb in allPendingBills)
                        {
                            pb.Status = 3; // 3 = Cancelled
                        }
                    }

                    if (isPreview)
                    {
                        var allBills = await _context.ParticipantBills.Include(b => b.BillLineItems).Where(b => b.SessionId == session.SessionId && b.UserId == userId && b.WalkinId == walkinId && b.Status != 3).ToListAsync();
                        var allCustomItems = allBills.SelectMany(b => b.BillLineItems).Where(li => li.Description != "ค่าสนาม" && li.Description != "ค่าคอร์ท" && li.Description != "ค่าธรรมเนียม" && !li.Description.StartsWith("ค่าลูกแบด"));
                        foreach (var item in allCustomItems)
                        {
                            lineItems.Add(new BillLineItem { Description = item.Description, Amount = item.Amount });
                            totalAmount += item.Amount;
                        }
                    }

                    if (totalAmount < 0) totalAmount = 0;

                    // --- ถ้าเป็น Preview ให้ส่งกลับเลย ไม่ต้องบันทึก ---
                    if (isPreview)
                    {
                        return new BillSummaryDto
                        {
                            BillId = 0, // Dummy ID
                            TotalAmount = totalAmount,
                            LineItems = lineItems.Select(li => new BillLineItemDto { Description = li.Description, Amount = li.Amount }).ToList()
                        };
                    }

                    // --- ถ้าไม่ใช่ Preview ให้บันทึกลง DB ---
                    var newBill = new ParticipantBill
                    {
                        SessionId = session.SessionId,
                        UserId = userId,
                        WalkinId = walkinId,
                        TotalAmount = totalAmount,
                        Status = (byte)(totalAmount <= 0 ? 2 : 1), // ถ้า 0 บาท ให้ถือว่าจ่ายแล้ว
                        CreatedDate = DateTime.UtcNow
                    };
                    await _context.ParticipantBills.AddAsync(newBill);
                    await _context.SaveChangesAsync();

                    foreach (var item in lineItems) item.BillId = newBill.BillId;
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
            });
        }

        // --- NEW: ฟังก์ชันบันทึกการจ่ายเงิน ---
        public async Task<(bool Success, string Message, string? QrCodeStr)> PayBillAsync(int billId, int organizerUserId, PaymentRequestDto dto)
        {
            var bill = await _context.ParticipantBills
                .Include(b => b.Session).ThenInclude(s => s.CreatedByUser).ThenInclude(u => u.OrganizerProfile)
                .Include(b => b.BillLineItems)
                .FirstOrDefaultAsync(b => b.BillId == billId);

            if (bill == null || bill.Session.CreatedByUserId != organizerUserId) return (false, "Bill not found", null);

            if (dto.PaymentMethod == "QR Code")
            {
                var subAccountId = bill.Session.CreatedByUser?.OrganizerProfile?.XenditAccountId;
                string? qrCodeStr = await _xenditService.CreateQrCodeAsync($"BILL-{billId}", dto.Amount, subAccountId);
                if (string.IsNullOrEmpty(qrCodeStr))
                {
                    return (false, "ไม่สามารถสร้าง QR Code จาก Xendit ได้", null);
                }
                return (true, "QR Code generated", qrCodeStr);
            }

            // 1. อัปเดตสถานะบิลเป็นจ่ายแล้ว (Status = 2)
            bill.Status = 2;

            // 2. บันทึกประวัติการจ่ายเงิน (ถ้ามีตาราง Payments)
            var payment = new Payment
            {
                BillId = billId,
                PaymentMethod = dto.PaymentMethod == "QR Code" ? (byte)2 : (byte)1, // 1=Cash, 2=QR
                Amount = dto.Amount,
                PaymentDate = DateTime.UtcNow,
                ReceivedByUserId = organizerUserId
            };

            // หมายเหตุ: ต้องแน่ใจว่า DbContext มี DbSet<Payment> Payments
            await _context.Payments.AddAsync(payment);

            // --- FIX: หักค่าธรรมเนียมแพลตฟอร์มจาก Wallet ผู้จัด (กรณีผู้จัดกดยืนยันรับเงินสด/โอนตรง) ---
            var serviceFeeItem = bill.BillLineItems.FirstOrDefault(li => li.Description == "ค่าธรรมเนียม");
            decimal serviceFeeDeduct = serviceFeeItem?.Amount ?? 0;
            if (serviceFeeDeduct > 0)
            {
                var organizerWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == organizerUserId);
                if (organizerWallet == null)
                {
                    organizerWallet = new UserWallet { UserId = organizerUserId, Balance = 0 };
                    await _context.UserWallets.AddAsync(organizerWallet);
                }
                organizerWallet.Balance -= serviceFeeDeduct; // ยอมให้ยอดติดลบเป็นหนี้
                organizerWallet.UpdatedDate = DateTime.UtcNow;
                await _context.WalletTransactions.AddAsync(new WalletTransaction { 
                    Wallet = organizerWallet, Amount = serviceFeeDeduct, TransactionType = 2, // 2 = OUT
                    Description = $"หักค่าธรรมเนียมแอป (ผู้จัดรับชำระเอง): {bill.Session.GroupName}", ReferenceId = bill.SessionId 
                });
            }

            await _context.SaveChangesAsync();

            // --- แจ้งเตือนผู้เล่นว่าได้รับการชำระเงินแล้ว ---
            if (bill.UserId.HasValue)
            {
                var organizer = await _context.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.UserId == organizerUserId);

                await _notificationService.SendNotificationAsync(
                    bill.UserId.Value,
                    "ยืนยันการชำระเงิน",
                    $"'{organizer?.UserProfile?.Nickname ?? "ผู้จัด"}' ได้รับชำระเงินค่าก๊วน '{bill.Session.GroupName}' จำนวน {dto.Amount:N2} บาท ผ่าน {dto.PaymentMethod} เรียบร้อยแล้ว",
                    "PAYMENT_CONFIRMED_BY_ORGANIZER",
                    bill.SessionId
                );
            }

            // --- NEW: เพิ่มการส่ง SignalR ให้อัปเดตกระดานและบอกแอปฝั่งผู้เล่น ---
            await BroadcastLiveStateChange(bill.SessionId, organizerUserId);
            if (bill.UserId.HasValue)
            {
                // ส่ง Event เข้า Group ด้วย เพื่อให้แอปผู้เล่น (ที่เปิดหน้ากระดานอยู่) เด้ง popup แล้วกลับหน้าหลัก
                await _hubContext.Clients.Group($"session-{bill.SessionId}").SendAsync("PlayerCheckedOut", bill.UserId.Value);
            }

            return (true, "Payment recorded successfully", null);
        }

        // --- NEW: ฟังก์ชันยกเลิกบิล (เพื่อไม่ให้ยอดทบกันเมื่อจ่ายใหม่) ---
        public async Task<bool> CancelBillAsync(int billId, int organizerUserId)
        {
            var bill = await _context.ParticipantBills
                .Include(b => b.Session)
                .FirstOrDefaultAsync(b => b.BillId == billId);

            if (bill == null || bill.Session.CreatedByUserId != organizerUserId) return false;

            bill.Status = 3; // 3 = Cancelled
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> ProcessQrPaymentWebhookAsync(string referenceId, decimal amount)
        {
            // 1. แกะรหัส BillId ออกจาก referenceId เช่น "BILL-123"
            if (!referenceId.StartsWith("BILL-")) return false;
            if (!int.TryParse(referenceId.Substring(5), out int billId)) return false;

            var bill = await _context.ParticipantBills
                .Include(b => b.Session)
                .Include(b => b.BillLineItems)
                .FirstOrDefaultAsync(b => b.BillId == billId);

            // ถ้าไม่มีบิล หรือจ่ายแล้ว ไม่ต้องทำอะไรซ้ำซ้อน
            if (bill == null || bill.Status == 2) return true; 

            bill.Status = 2; // อัปเดตสถานะบิลเป็น 2 (จ่ายแล้ว)

            var payment = new Payment
            {
                BillId = billId,
                PaymentMethod = 2, // 2 = QR Code
                Amount = amount,
                PaymentDate = DateTime.UtcNow,
                ReceivedByUserId = bill.Session.CreatedByUserId // ผู้รับเงินคือเจ้าของก๊วน
            };

            await _context.Payments.AddAsync(payment);

            // --- FIX: หักค่าธรรมเนียมแพลตฟอร์มจาก Wallet ผู้จัด (กรณีลูกค้าสแกน QR เข้า Sub-Account สำเร็จ) ---
            var serviceFeeItem = bill.BillLineItems.FirstOrDefault(li => li.Description == "ค่าธรรมเนียม");
            decimal serviceFeeDeduct = serviceFeeItem?.Amount ?? 0;
            if (serviceFeeDeduct > 0)
            {
                var organizerWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == bill.Session.CreatedByUserId);
                if (organizerWallet == null)
                {
                    organizerWallet = new UserWallet { UserId = bill.Session.CreatedByUserId, Balance = 0 };
                    await _context.UserWallets.AddAsync(organizerWallet);
                }
                organizerWallet.Balance -= serviceFeeDeduct; // ยอมให้ยอดติดลบเป็นหนี้
                organizerWallet.UpdatedDate = DateTime.UtcNow;
                await _context.WalletTransactions.AddAsync(new WalletTransaction { 
                    Wallet = organizerWallet, Amount = serviceFeeDeduct, TransactionType = 2, // 2 = OUT
                    Description = $"หักค่าธรรมเนียมแอป (รับเงินผ่าน QR): {bill.Session.GroupName}", ReferenceId = bill.SessionId 
                });
            }

            await _context.SaveChangesAsync();

            // 2. ส่ง SignalR กลับไปหาผู้เล่น ให้แอปปิดหน้าต่าง QR Code ทันที
            if (bill.UserId.HasValue)
            {
                // เปลี่ยนจากการยิงเข้า Group เป็นการยิงหา User คนนั้นโดยตรงผ่าน Connection ของเขา
                await _hubContext.Clients.User(bill.UserId.Value.ToString()).SendAsync("QrPaymentSuccess", bill.BillId);
                await _notificationService.SendNotificationAsync(bill.UserId.Value, "ชำระเงินสำเร็จ", $"ระบบได้รับยอดชำระเงิน {amount:N2} บาท ผ่าน QR Code เรียบร้อยแล้ว", "PAYMENT_SUCCESS", bill.SessionId);
            }
            
            // ส่งเข้า Group ด้วย เผื่อกรณีผู้จัดเปิด QR Code เองให้ Walk-in สแกน หน้าจอผู้จัดจะได้ปิดอัตโนมัติเช่นกัน
            await _hubContext.Clients.Group($"session-{bill.SessionId}").SendAsync("QrPaymentSuccess", bill.BillId);

            // 3. ส่ง SignalR อัปเดตกระดาน (Live State) ฝั่งผู้จัด เพื่อให้เห็นว่ายอดเงินเข้าแล้ว
            await BroadcastLiveStateChange(bill.SessionId, bill.Session.CreatedByUserId);
            return true;
        }
    }
}
