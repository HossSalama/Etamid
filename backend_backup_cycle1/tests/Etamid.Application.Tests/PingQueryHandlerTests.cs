using Etamid.Application.Common.Interfaces;
using Etamid.Application.Platform.Queries.Ping;
using NSubstitute;

namespace Etamid.Application.Tests;

public class PingQueryHandlerTests
{
    [Fact]
    public async Task Returns_ok_with_server_time()
    {
        var now = new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);

        var result = await new PingQueryHandler(clock).Handle(new PingQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("ok");
        result.Value.ServerTimeUtc.Should().Be(now);
    }
}
