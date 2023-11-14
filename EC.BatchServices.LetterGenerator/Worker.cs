using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ReportGenerator _reportGenerator;

    public Worker(ILogger<Worker> logger, ReportGenerator reportGenerator)
    {
        _logger = logger;
        _reportGenerator = reportGenerator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                _logger.LogInformation("Checking for report queue for work...");
                var isWorkReady = await _reportGenerator.IsWorkPendingAsync();
                if (isWorkReady)
                {
                    _logger.LogInformation("Work is pending. Starting report generation...");
                    await _reportGenerator.GenerateAndSaveReportsAsync();
                }
                else
                {
                    _logger.LogInformation("No work is pending. Returning to sleep state.");
                }
                _logger.LogInformation("Waiting for 1 hour before the next execution.");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while generating the report.");
            }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
