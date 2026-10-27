using Etamid.Application.Deals.Commands;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Etamid.Infrastructure.Jobs;

/// <summary>
/// Every minute: releases escrow whose inspection window ended without a dispute, and expires deals nobody
/// answered or funded in time. Each deal runs in its own scope so one failure never blocks the others.
/// Disputed deals are never selected, so a dispute always stops the automatic release.
/// </summary>
public sealed class EscrowMaintenanceWorker(
    IServiceScopeFactory scopes, IConfiguration configuration, ILogger<EscrowMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Max(5, configuration.GetValue("Escrow:MaintenanceIntervalSeconds", 60));
        var interval = TimeSpan.FromSeconds(seconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Escrow maintenance run failed.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        DueDealsDto due;
        using (var scope = scopes.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetDueDealsQuery(), ct);
            if (result.IsFailure) return;
            due = result.Value;
        }

        foreach (var dealId in due.ReleaseDealIds)
            await RunAsync(new AutoReleaseDealCommand(dealId), "auto-release", dealId, ct);

        foreach (var dealId in due.ExpireDealIds)
            await RunAsync(new ExpireDealCommand(dealId), "expire", dealId, ct);
    }

    private async Task RunAsync(IRequest<Etamid.SharedKernel.Result> command, string what, long dealId, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, ct);
            if (result.IsFailure)
                logger.LogWarning("Deal {DealId}: {What} skipped ({Code}).", dealId, what, result.Error.Code);
            else
                logger.LogInformation("Deal {DealId}: {What} done.", dealId, what);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deal {DealId}: {What} failed.", dealId, what);
        }
    }
}
