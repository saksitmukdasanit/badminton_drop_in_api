#nullable disable warnings
namespace DropInBadAPI.Dtos
{
    public class ParticipantFinancialDto
    {
        public int ParticipantId { get; set; }
        public string ParticipantType { get; set; }
        public string Nickname { get; set; }
        public string? Name { get; set; }
        public int GamesPlayed { get; set; }
        public decimal TotalCost { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
        public decimal CourtFee { get; set; }
        public decimal ShuttleFee { get; set; }

        // --- NEW: ยอดสุทธิสำหรับผู้จัดรายคน ---
        public decimal ServiceFee { get; set; }
        public decimal OrganizerNetTotal { get; set; }
        public decimal OrganizerNetPaid { get; set; }
        public decimal OrganizerNetUnpaid { get; set; }
    }
}