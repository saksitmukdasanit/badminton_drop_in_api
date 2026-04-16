using System;

namespace DropInBadAPI.Models
{
    public class UserFollow
    {
        public int FollowerId { get; set; }
        public int OrganizerId { get; set; }
        public DateTime CreatedDate { get; set; }
        public virtual User Follower { get; set; } = null!;
        public virtual User Organizer { get; set; } = null!;
    }
}