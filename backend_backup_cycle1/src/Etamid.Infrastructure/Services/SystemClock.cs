using Etamid.Application.Common.Interfaces;

namespace Etamid.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
