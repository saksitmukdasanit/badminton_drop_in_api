namespace DropInBadAPI.Dtos
{
    public record LoginResponseDto(string AccessToken, string RefreshToken);
    public record RefreshTokenDto(string AccessToken, string RefreshToken);
    // ข้อมูลที่รับเข้ามาตอนสมัครสมาชิก
    public record InitiateRegisterDto(string PhoneNumber, string Username, string Password);
    public record VerifyOtpDto(string PhoneNumber, string OtpCode);
    public record CompleteProfileDto(
        string Nickname,
        string FirstName,
        string LastName,
        string Email,
        int Gender,
        string? ProfilePhotoUrl,
        string? EmergencyContactName,
        string? EmergencyContactPhone
    );

    // ข้อมูลที่รับเข้ามาตอนล็อกอิน
    public record LoginDto(string Username, string Password);

    // ข้อมูลที่ส่งกลับไปเมื่อล็อกอินสำเร็จ (แบบไม่มี Token)
    public record SimpleLoginSuccessDto(int UserId, string Nickname, string Message);

    // ข้อมูลโปรไฟล์ที่จะส่งกลับไป (เพื่อไม่เปิดเผยข้อมูลที่ไม่จำเป็น)
    public record UserProfileDto(int UserId, string? Nickname, string? ProfilePhotoUrl, string? Email);
    public record ChangePasswordDto(string OldPassword, string NewPassword);

    public record RequestOtpDto(string PhoneNumber);

    // DTO สำหรับคืนค่า Token พิเศษ (สำหรับ Reset Password)
    public record ResetTokenResponseDto(string ResetToken);

    // DTO สำหรับตั้งรหัสผ่านใหม่
    public record ResetPasswordDto(string NewPassword);
}