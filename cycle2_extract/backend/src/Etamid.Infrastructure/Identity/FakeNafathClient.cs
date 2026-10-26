using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Identity;
using Microsoft.Extensions.Configuration;

namespace Etamid.Infrastructure.Identity;

/// <summary>
/// Development stand-in for Nafath. A request is "approved" a few seconds after it starts.
/// The real integration needs onboarding with Elm and replaces this class.
/// </summary>
public sealed class FakeNafathClient(IConfiguration configuration, IClock clock) : INafathClient
{
    public Task<NafathStartResult> StartAsync(string requestId, CancellationToken cancellationToken)
        => Task.FromResult(new NafathStartResult(Random.Shared.Next(10, 100).ToString()));

    public Task<NafathPollResult> PollAsync(string requestId, DateTime startedAtUtc, CancellationToken cancellationToken)
    {
        var approveAfter = configuration.GetValue<int?>("Nafath:FakeApproveAfterSeconds") ?? 5;
        var approved = (clock.UtcNow - startedAtUtc).TotalSeconds >= approveAfter;

        return Task.FromResult(approved
            ? new NafathPollResult(NafathStatus.Verified, "1000000001", "Test User")
            : new NafathPollResult(NafathStatus.Pending, null, null));
    }
}
