using Daisy.Application.Features.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Daisy.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs UC-72 every minute. It only schedules the work; the use case is
/// OrderExpiryService.CancelExpiredOrdersAsync (ARCHITECTURE §17, BR-41).
/// Safe on several API instances: each cancel is a conditional update.
/// </summary>
public sealed class UnpaidOrderExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<UnpaidOrderExpiryJob> _logger;

    public UnpaidOrderExpiryJob(IServiceScopeFactory scopes, ILogger<UnpaidOrderExpiryJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<OrderExpiryService>();
                await service.CancelExpiredOrdersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The unpaid order expiry job failed; it will run again in {Interval}.", Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
