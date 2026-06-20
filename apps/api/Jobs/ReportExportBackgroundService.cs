using Microsoft.Extensions.Options;
using MidiKaval.Api.Infrastructure.Reports;

namespace MidiKaval.Api.Jobs;

public sealed class ReportExportBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReportExportOptions> options,
    ILogger<ReportExportBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<ReportExportJobRunner>();
                await runner.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Report export background job failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
