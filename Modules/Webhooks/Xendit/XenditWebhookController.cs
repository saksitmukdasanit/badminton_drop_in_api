using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using DropInBadAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace DropInBadAPI.Controllers.Webhooks
{
    [ApiController]
    [Route("api/webhooks/xendit")]
    [AllowAnonymous] // สำคัญมาก: Xendit ไม่มี Token ของเรา ต้อง AllowAnonymous
    public class XenditWebhookController : ControllerBase
    {
        private readonly IMatchManagementService _matchService;
        private readonly IConfiguration _configuration;

        public XenditWebhookController(IMatchManagementService matchService, IConfiguration configuration)
        {
            _matchService = matchService;
            _configuration = configuration;
        }

        [HttpPost("qr-payment")]
        public async Task<IActionResult> HandleQrPayment()
        {
            // 1. ตรวจสอบ Webhook Token เพื่อความปลอดภัย (ป้องกันคนยิง API มั่ว)
            var webhookToken = _configuration["Xendit:WebhookVerificationToken"];
            if (!string.IsNullOrEmpty(webhookToken) && Request.Headers.TryGetValue("x-callback-token", out var tokenReceived))
            {
                if (tokenReceived != webhookToken) return Unauthorized("Invalid webhook token");
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var eventType = root.TryGetProperty("event", out var e) ? e.GetString() : "";

                // เช็คว่าเป็น Event การจ่ายเงิน QR สำเร็จ
                if (eventType == "qr.payment" || eventType == "qr.payment.succeeded" || string.IsNullOrEmpty(eventType))
                {
                    var data = root.TryGetProperty("data", out var d) ? d : root;
                    var status = data.TryGetProperty("status", out var s) ? s.GetString() : "";

                    if (status == "COMPLETED" || status == "SUCCEEDED" || status == "PAID")
                    {
                        string referenceId = "";
                        decimal amount = 0;

                        // ใช้ TryGetProperty เพื่อป้องกัน Error 400 เวลารูปแบบ JSON ไม่ตรง
                        if (data.TryGetProperty("reference_id", out var refIdProp))
                            referenceId = refIdProp.GetString() ?? "";
                        else if (data.TryGetProperty("external_id", out var extIdProp)) // รองรับ Payload ของ Invoice ด้วย
                            referenceId = extIdProp.GetString() ?? "";

                        if (data.TryGetProperty("amount", out var amtProp))
                            amount = amtProp.GetDecimal();
                        else if (data.TryGetProperty("paid_amount", out var paidAmtProp)) // รองรับ Payload ของ Invoice ด้วย
                            amount = paidAmtProp.GetDecimal();

                        if (!string.IsNullOrEmpty(referenceId)) 
                            await _matchService.ProcessQrPaymentWebhookAsync(referenceId, amount);
                    }
                }
                return Ok(new { status = "success" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Xendit Webhook Error: {ex.Message}");
                return BadRequest("Error processing webhook");
            }
        }
    }
}