namespace DropInBadAPI.Dtos
{
    public class GameSessionFinancialsDto
    {
        public int SessionId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int CurrentParticipants { get; set; }
        public decimal CourtFeePerPerson { get; set; }
        public decimal ShuttlecockFeePerPerson { get; set; }
        public decimal ShuttlecockCostPerUnit { get; set; }
        public decimal TotalCourtCost { get; set; }
        public decimal TotalCourtIncome { get; set; }
        public decimal TotalShuttlecockFee { get; set; }
        public decimal TotalShuttlecockCost { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal TotalCashAmount { get; set; }
        public decimal TotalTransferAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
        public int TotalShuttlecocks { get; set; }

        // From ManageDtos
        public int PaidCourtCount { get; set; }
        public int UnpaidCourtCount { get; set; }
        public decimal PaidCourtAmount { get; set; }
        public decimal UnpaidCourtAmount { get; set; }
        public decimal PaidShuttleAmount { get; set; }
        public decimal UnpaidShuttleAmount { get; set; }
        public decimal TotalAdditions { get; set; }
        public decimal TotalSubtractions { get; set; }

        public List<ParticipantFinancialDto> Participants { get; set; } = new();
    }

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
    }
}