using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DropInBadAPI.Service.Mobile.Game;

/// <summary>
/// รันทุก 30 นาที เพื่อสแกนก๊วนที่เลยเวลา EndTime แล้ว ผู้จัดไม่ได้กดเริ่ม
/// ระบบจะ Auto-Cancel + คืนเงินผู้เล่นอัตโนมัติ
/// </summary>
public class ExpiredSessionCancellationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredSessionCancellationHostedService> _logger;

    public ExpiredSessionCancellationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredSessionCancellationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // รอ 60 วินาทีก่อนรอบแรก เพื่อให้ระบบ startup เสร็จก่อน
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IGameSessionService>();
                var cancelled = await svc.AutoCancelExpiredSessionsAsync(stoppingToken);
                if (cancelled > 0)
                    _logger.LogInformation("[AutoCancel] ยกเลิกก๊วนหมดเวลาอัตโนมัติ {N} ก๊วน พร้อมคืนเงินผู้เล่นเรียบร้อย", cancelled);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AutoCancel] เกิดข้อผิดพลาดในการสแกนก๊วนหมดเวลา");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
