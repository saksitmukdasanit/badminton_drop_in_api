using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DropInBadAPI.Service.Mobile.Game;

/// <summary>รันซ้ำทุก 6 ชม. เพื่อเติมก๊วนจาก template ในช่วง ~14 วันข้างหน้า</summary>
public class RecurringSessionGenerationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringSessionGenerationHostedService> _logger;

    public RecurringSessionGenerationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecurringSessionGenerationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IRecurringGameTemplateService>();
                var n = await svc.GenerateMissingSessionsAsync(null, stoppingToken);
                if (n > 0)
                    _logger.LogInformation("Recurring hosted job created {N} sessions", n);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Recurring hosted job failed");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
