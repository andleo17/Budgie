using Budgie.Modules.EmailImport.Application.Inbox;
using Budgie.Modules.EmailImport.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Budgie.Modules.EmailImport.Infrastructure.Worker;

/// <summary>
/// Background ticker for email synchronisation (spec §2.4). Deliberately dumb: it owns
/// the timer, opens one DI scope per tick and hands the work over to
/// <see cref="IEmailSyncService"/>. All sync logic lives in the service, so integration
/// tests invoke it directly — no waiting, no flakiness.
/// </summary>
/// <remarks>
/// The first tick waits <see cref="InitialDelay"/> so the sync never races application
/// startup (schema scripts, provider registrations, database availability). A failing
/// tick is logged (EventId 17) and swallowed: the timer must outlive any single failure.
/// </remarks>
internal sealed class EmailSyncWorker(
    ILogger<EmailSyncWorker> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<EmailImportOptions> options) : BackgroundService
{
    /// <summary>Delay before the first tick, to stay out of the application's way while it boots.</summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return; // belt-and-braces: slice 13 registers the hosted service conditionally

        var interval = TimeSpan.FromMinutes(Math.Max(1, settings.SyncIntervalMinutes));

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return; // shutdown before the first tick
        }

        using var timer = new PeriodicTimer(interval);
        do
        {
            await RunTickAsync(stoppingToken);
        } while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        try
        {
            // One scope per tick: the sync service, the repositories and
            // ScopedCurrentUserProvider live and die with the tick.
            await using var scope = scopeFactory.CreateAsyncScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IEmailSyncService>();
            await syncService.SyncAllAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down; not an error.
        }
        catch (Exception ex)
        {
            // A failing tick must never kill the timer (spec §9: resilience over fail-fast
            // here — the next tick is the retry).
            LogMessages.SyncTickFailed(logger, ex);
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false; // shutdown between ticks
        }
    }
}
