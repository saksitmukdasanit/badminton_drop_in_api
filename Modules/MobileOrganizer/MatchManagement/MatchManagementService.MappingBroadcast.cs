using Microsoft.Extensions.Configuration;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Hubs;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Services
{
    public partial class MatchManagementService
    {
        private async Task BroadcastLiveStateChange(int sessionId, int organizerUserId)
        {
            // ใช้ IServiceProvider เพื่อ resolve service และหลีกเลี่ยง circular dependency
            using (var scope = _serviceProvider.CreateScope())
            {
                var matchService = scope.ServiceProvider.GetRequiredService<IMatchManagementService>();
                var liveState = await matchService.GetLiveStateAsync(sessionId, organizerUserId);
                if (liveState != null)
                {
                    await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("ReceiveLiveStateUpdate", liveState);
                }
            }
        }

        // --- Helper Methods ---
        private PlayerInMatchDto MapToPlayerInMatchDto(MatchPlayer p)
        {
            var sessionParticipant = p.User?.SessionParticipants.FirstOrDefault();
            return new PlayerInMatchDto
            {
                UserId = sessionParticipant?.ParticipantId ?? p.UserId, // ส่ง ParticipantId กลับไปเพื่อให้ ID ตรงกับตอน Waiting
                WalkinId = p.WalkinId,
                Nickname = p.UserId.HasValue ? p.User?.UserProfile?.Nickname ?? "N/A" : p.Walkin?.GuestName ?? "N/A",
                ProfilePhotoUrl = p.UserId.HasValue ? p.User?.UserProfile?.ProfilePhotoUrl : null,
                GenderName = p.UserId.HasValue ? (p.User?.UserProfile?.Gender == 1 ? "ชาย" : p.User?.UserProfile?.Gender == 2 ? "หญิง" : "อื่นๆ") : (p.Walkin?.Gender == 1 ? "ชาย" : p.Walkin?.Gender == 2 ? "หญิง" : "อื่นๆ"),
                SkillLevelId = p.UserId.HasValue ? sessionParticipant?.SkillLevelId : p.Walkin?.SkillLevelId,
                SkillLevelName = p.UserId.HasValue ? sessionParticipant?.SkillLevel?.LevelName : p.Walkin?.SkillLevel?.LevelName,
                SkillLevelColor = p.UserId.HasValue ? sessionParticipant?.SkillLevel?.ColorHexCode : p.Walkin?.SkillLevel?.ColorHexCode,
                EmergencyContactName = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactName : null,
                EmergencyContactPhone = p.UserId.HasValue ? p.User?.UserProfile?.EmergencyContactPhone : null
            };
        }

        private void ProcessDuplicateNames(IEnumerable<dynamic> players)
        {
            var duplicateGroups = players
                .Where(p => p.Nickname != null && p.Nickname != "")
                .GroupBy(p => (string)p.Nickname)
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                int counter = 1;
                foreach (var player in group)
                {
                    player.Nickname = $"{player.Nickname} ({counter++})";
                }
            }
        }
    }
}
