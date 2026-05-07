using Microsoft.Extensions.Configuration;
using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Hubs;
using DropInBadAPI.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace DropInBadAPI.Services
{
    /// <summary>
    /// ฟาซาดหลัก — dependencies อยู่ที่นี่; implementation แยกตามโดเมนในไฟล์ partial อื่น
    /// </summary>
    public partial class MatchManagementService : IMatchManagementService
    {
        private readonly BadmintonDbContext _context;
        private readonly IHubContext<ManagementGameHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly INotificationService _notificationService;
        private readonly IXenditService _xenditService;

        public MatchManagementService(
            BadmintonDbContext context,
            IHubContext<ManagementGameHub> hubContext,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            INotificationService notificationService,
            IXenditService xenditService)
        {
            _context = context;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _notificationService = notificationService;
            _xenditService = xenditService;
        }
    }
}
