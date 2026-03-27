namespace DropInBadAPI.Dtos
{
    

    public class MatchPerformanceDto
    {
        public string Players { get; set; } = string.Empty; // "Name A1, Name A2 vs Name B1, Name B2"
        public string Duration { get; set; } = string.Empty; // "xx นาที"
    }

    public class MatchHistoryDto
    {
        public int MatchId { get; set; }
        public string CourtNumber { get; set; } = string.Empty;
        public int ShuttlecocksUsed { get; set; }
        public string TeamA { get; set; } = string.Empty;
        public string TeamB { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
    }
}