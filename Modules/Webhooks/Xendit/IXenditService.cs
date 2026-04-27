namespace DropInBadAPI.Interfaces
{
    public interface IXenditService
    {
        // ฟังก์ชันสั่งสร้าง QR Code โดยส่ง รหัสบิล (อ้างอิง) และ ยอดเงิน เข้าไป
        Task<string?> CreateQrCodeAsync(string referenceId, decimal amount, string? subAccountId = null);
        
        // ฟังก์ชันสร้างบัญชีลูก (Sub-account) สำหรับผู้จัด
        Task<string?> CreateSubAccountAsync(string email, string businessName);
        
        // ฟังก์ชันสร้างรายการถอนเงิน (Payout) โอนเข้าบัญชีธนาคาร
        Task<(bool Success, string Message, string? PayoutId)> CreatePayoutAsync(string referenceId, decimal amount, string bankCode, string accountName, string accountNumber, string description, string? subAccountId = null);
    }
}