namespace DropInBadAPI.Utility
{
    public class Helper
    {
        //status code
        public static int SuccessCode = 1;
        public static int FailureCode = 0;
        public static int WarningCode = 2;
        public static int DuplicateCode = 3;

        //status message
        public static string SaveSuccess = "บันทึกข้อมูลสำเร็จ";
        public static string SaveFailure = "บันทึกข้อมูลไม่สำเร็จ";
        public static string OtpExpire = "รหัส OTP หมดอายุ";
        public static string OtpNotValid = "รหัส OTP ไม่ถูกต้อง";
    }
}
