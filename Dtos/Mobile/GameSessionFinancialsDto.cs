namespace DropInBadAPI.Dtos
{
    public class GameSessionFinancialsDto
    {
        public int SessionId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int CurrentParticipants { get; set; }
        public decimal CourtFeePerPerson { get; set; }
        public decimal ShuttlecockFeePerPerson { get; set; }
        public decimal TotalCourtCost { get; set; } // ต้นทุนค่าสนาม
        public decimal TotalCourtIncome { get; set; } // รายได้ค่าสนาม
        public decimal TotalShuttlecockFee { get; set; } // รายได้ค่าลูก
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
        public int TotalShuttlecocks { get; set; }
        public List<ParticipantFinancialDto> Participants { get; set; } = new();
    }

    public class ParticipantFinancialDto
    {
        public int ParticipantId { get; set; }
        public string ParticipantType { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string? Name { get; set; }
        public int GamesPlayed { get; set; }
        public int ShuttlecocksUsed { get; set; }
        public decimal TotalCost { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
    }
}