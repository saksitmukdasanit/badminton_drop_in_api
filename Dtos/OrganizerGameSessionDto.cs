namespace DropInBadAPI.Dtos
{
    public class OrganizerGameSessionDto
    {
        public int GameSessionId { get; set; }
        public DateTime Date { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public decimal TotalIncome { get; set; } // รายได้รวม
        public decimal PaidAmount { get; set; }  // จ่ายแล้ว
        public decimal UnpaidAmount { get; set; } // ค้างจ่าย
        public string Status { get; set; } = string.Empty; // สถานะก๊วน (เช่น จบแล้ว, กำลังจะถึง)
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int TotalParticipants { get; set; }
        public int? TotalCourts { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}