namespace DropInBadAPI.Dtos
{
    public class GameSessionAnalyticsDto
    {
        public string GroupName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int TotalGames { get; set; }
        public int TotalShuttlecocks { get; set; } // จำนวนลูกที่ใช้ทั้งหมด
        public string TotalPlayTimeStart { get; set; } = string.Empty; // เวลาเริ่มตีทั้งหมด (HH:mm)
        public string TotalPlayTimeEnd { get; set; } = string.Empty;   // เวลาสิ้นสุดการตีทั้งหมด (HH:mm)
        public string AveragePlayTimePerGame { get; set; } = string.Empty; // เวลาตีต่อเกมเฉลี่ย (mm:ss)
        
        public MatchPerformanceDto? LongestGame { get; set; }
        public MatchPerformanceDto? ShortestGame { get; set; }
        
        public List<MatchHistoryDto> MatchHistory { get; set; } = new();
    }

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