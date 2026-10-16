using Microsoft.Extensions.Options;

namespace Desk.Scheduler;

// Runs the jobs every few minutes. Separate from the web app so a web problem can
// never stop emails and reminders (and the other way round).
public class Worker(IServiceScopeFactory scopes, IOptions<SchedulerOptions> options, ILogger<Worker> log,
    IHostApplicationLifetime lifetime, RunMode mode) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        do
        {
            await RunOnceAsync(stoppingToken);
            if (mode.Once) { lifetime.StopApplication(); return; }
            try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes)), stoppingToken); }
            catch (OperationCanceledException) { return; }
        } while (!stoppingToken.IsCancellationRequested);
    }

    async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<MailJob>();
            var firsts = await job.FirstSignInMailAsync(ct);
            if (firsts > 0) log.LogInformation("First sign-in emails sent: {Sent}", firsts);
            await Heartbeat($"ok {DateTime.UtcNow:O} firstSignIns={firsts}", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never crash the service; log and try again next run.
            log.LogError(ex, "Scheduler run failed");
            await Heartbeat($"error {DateTime.UtcNow:O} {ex.GetType().Name}: {ex.Message}", ct);
        }
    }

    async Task Heartbeat(string line, CancellationToken ct)
    {
        try
        {
            var file = options.Value.HeartbeatFile;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, line, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Could not write heartbeat");
        }
    }
}

public record RunMode(bool Once);
