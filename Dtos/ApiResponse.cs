namespace DropInBadAPI.Models
{
    // T คือ Generic Type หมายถึง Data สามารถเป็นอะไรก็ได้
    public class Response<T>
    {
        // สถานะของ Response (อาจจะใช้ HTTP Status Code หรือโค้ดภายในของคุณเอง)
        public int Status { get; set; }

        // ข้อความอธิบาย
        public string Message { get; set; } = string.Empty;

        // ข้อมูลหลักที่จะส่งกลับไป
        public T? Data { get; set; }

        // จำนวนข้อมูลทั้งหมด (สำหรับใช้กับการแบ่งหน้า - Pagination)
        public long? Total { get; set; }
    }
}