using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;

namespace DropInBadAPI.Hubs
{
    [Authorize] // <<<<<<<<<<<<<<< เพิ่มบรรทัดนี้
    public class ManagementGameHub : Hub
    {
        // เมธอดที่ Client (Flutter) จะเรียกเมื่อเข้ามาในหน้า Live State
        public async Task JoinSessionGroup(String sessionId)
        {
            try
            {
                // นำ Connection ปัจจุบันเข้ากลุ่มตาม sessionId
                await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sessionId}");
            }
            catch (Exception ex)
            {
                // Log a more detailed error message on the server
                Console.WriteLine($"Error in JoinSessionGroup for session {sessionId}: {ex.Message}");
                throw; // Re-throw the exception to notify the client
            }
        }

        // เมธอดที่ Client (Flutter) จะเรียกเมื่อออกจากหน้า Live State
        public async Task LeaveSessionGroup(String sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session-{sessionId}");
        }
    }
}