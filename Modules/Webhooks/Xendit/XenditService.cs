using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using DropInBadAPI.Interfaces;

namespace DropInBadAPI.Services
{
    public class XenditService : IXenditService
    {
        private readonly HttpClient _httpClient;
        private readonly string _secretKey;

        public XenditService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _secretKey = configuration["Xendit:SecretKey"] ?? "";
            
            // ตั้งค่า Basic Authentication โดยใช้ Secret Key รหัสผ่านเว้นว่างไว้
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_secretKey}:"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            _httpClient.DefaultRequestHeaders.Add("api-version", "2022-07-31");
        }

          public async Task<string?> CreateQrCodeAsync(string referenceId, decimal amount, string? subAccountId = null)
        {
            if (string.IsNullOrEmpty(_secretKey)) return null;

            var requestBody = new
            {
                reference_id = referenceId, // รหัสบิลของเรา เช่น "Bill_123"
                type = "DYNAMIC", // Dynamic คือการล็อคยอดเงินมากับ QR เลย (ลูกค้าแก้เลขไม่ได้)
                currency = "THB",
                amount = amount,
                expires_at = DateTime.UtcNow.AddMinutes(15).ToString("yyyy-MM-ddTHH:mm:ssZ") // ให้ QR หมดอายุใน 15 นาที
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                   
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.xendit.co/qr_codes")
            {
                Content = content
            };
            
            // --- NEW: ถ้ามี Sub-Account ให้ผูกบิลเข้ากับบัญชีผู้จัดโดยตรง ---
            if (!string.IsNullOrEmpty(subAccountId))
            {
                request.Headers.Add("for-user-id", subAccountId);
            }

            var response = await _httpClient.SendAsync(request);
            
            
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                return doc.RootElement.GetProperty("qr_string").GetString(); // คืนค่า String ไปให้ Flutter วาด QR
            }

            // ถ้าสร้างไม่สำเร็จ (เช่น ยอดเงินน้อยกว่า 1 บาท หรือ Key ผิด)
            var errorStr = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Xendit Error: {errorStr}");
            return null;
        }

        public async Task<string?> CreateSubAccountAsync(string email, string businessName)
        {
            if (string.IsNullOrEmpty(_secretKey)) return null;

            var requestBody = new
            {
                email = email,
                type = "OWNED",
                business_profile = new { business_name = businessName }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.xendit.co/v2/accounts", content);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                return doc.RootElement.GetProperty("id").GetString(); // คืนค่า Account ID (เช่น 614ac13b2c6...)
            }

            return null;
        }

        public async Task<(bool Success, string Message, string? PayoutId)> CreatePayoutAsync(string referenceId, decimal amount, string bankCode, string accountName, string accountNumber, string description, string? subAccountId = null)
        {
            if (string.IsNullOrEmpty(_secretKey)) return (false, "Secret Key not configured", null);

            var requestBody = new
            {
                reference_id = referenceId,
                currency = "THB",
                channel_code = bankCode, // เช่น "TH_KASIKORN", "TH_SCB" (ต้องตรงกับตาราง Bank)
                channel_properties = new
                {
                    account_holder_name = accountName,
                    account_number = accountNumber
                },
                amount = amount,
                description = description
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.xendit.co/v2/payouts")
            {
                Content = content
            };

            if (!string.IsNullOrEmpty(subAccountId))
            {
                request.Headers.Add("for-user-id", subAccountId);
            }

            var response = await _httpClient.SendAsync(request);

            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseString);
                var payoutId = doc.RootElement.GetProperty("id").GetString();
                return (true, "Payout created successfully", payoutId);
            }

            using var errDoc = JsonDocument.Parse(responseString);
            var errMsg = errDoc.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown payout error";
            return (false, errMsg ?? "Unknown payout error", null);
        }
    }
}