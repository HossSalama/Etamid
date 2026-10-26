using Etamid.Application.Common.Interfaces;
using Etamid.SharedKernel;
using MediatR;

namespace Etamid.Application.Platform.Queries.Ping;

public sealed record PingQuery : IRequest<Result<PingDto>>;

public sealed record PingDto(string Status, DateTime ServerTimeUtc);

public sealed class PingQueryHandler(IClock clock) : IRequestHandler<PingQuery, Result<PingDto>>
{
    public Task<Result<PingDto>> Handle(PingQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success(new PingDto("ok", clock.UtcNow)));
}
