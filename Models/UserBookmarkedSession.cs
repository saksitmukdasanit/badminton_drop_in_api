using System;

namespace DropInBadAPI.Models
{
    public class UserBookmarkedSession
    {
        public int UserId { get; set; }
        public int SessionId { get; set; }
        public DateTime CreatedDate { get; set; }
        public virtual User User { get; set; } = null!;
        public virtual GameSession Session { get; set; } = null!;
    }
}