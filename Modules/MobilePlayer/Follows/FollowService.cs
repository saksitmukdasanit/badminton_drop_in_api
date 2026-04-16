using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DropInBadAPI.Services
{
    public class FollowService : IFollowService
    {
        private readonly BadmintonDbContext _context;

        public FollowService(BadmintonDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string ErrorMessage)> ToggleFollowAsync(int followerId, int organizerId)
        {
            var existingFollow = await _context.UserFollows
                .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.OrganizerId == organizerId);

            if (existingFollow != null) _context.UserFollows.Remove(existingFollow);
            else await _context.UserFollows.AddAsync(new UserFollow { FollowerId = followerId, OrganizerId = organizerId, CreatedDate = DateTime.UtcNow });

            await _context.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<IEnumerable<OrganizerSummaryDto>> GetFollowedOrganizersAsync(int followerId)
        {
            var followedOrganizerIds = await _context.UserFollows
                .Where(f => f.FollowerId == followerId)
                .Select(f => f.OrganizerId)
                .ToListAsync();

            var organizers = await _context.Users
                .Include(u => u.UserProfile)
                .Where(u => followedOrganizerIds.Contains(u.UserId))
                .ToListAsync();

            var result = new List<OrganizerSummaryDto>();
            foreach (var org in organizers)
            {
                var sessions = await _context.GameSessions.Where(s => s.CreatedByUserId == org.UserId).ToListAsync();
                result.Add(new OrganizerSummaryDto
                {
                    OrganizerId = org.UserId,
                    Nickname = org.UserProfile?.Nickname ?? "N/A",
                    ProfilePhotoUrl = org.UserProfile?.ProfilePhotoUrl,
                    TotalHosted = sessions.Count(s => s.Status != 3),
                    TotalCancelled = sessions.Count(s => s.Status == 3),
                    IsFollowed = true
                });
            }
            return result;
        }
    }
}