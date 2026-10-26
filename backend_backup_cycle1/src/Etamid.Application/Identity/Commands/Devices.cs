using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record RegisterDeviceCommand(string DeviceToken, string Platform) : IRequest<Result>;

public sealed class RegisterDeviceValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceValidator()
    {
        RuleFor(x => x.DeviceToken).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(500).WithErrorCode("TOO_LONG");
        RuleFor(x => x.Platform).Must(v => Enum.TryParse<DevicePlatform>(v, true, out _)).WithErrorCode("INVALID_VALUE");
    }
}

public sealed class RegisterDeviceHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<RegisterDeviceCommand, Result>
{
    public async Task<Result> Handle(RegisterDeviceCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var platform = Enum.Parse<DevicePlatform>(r.Platform, true);
        var device = await db.Devices.FirstOrDefaultAsync(d => d.DeviceToken == r.DeviceToken, ct);

        if (device is null)
        {
            db.Devices.Add(new Device
            {
                UserId = userId,
                DeviceToken = r.DeviceToken,
                Platform = platform,
                LastSeenAt = clock.UtcNow,
                IsActive = true
            });
        }
        else
        {
            // A phone that changes owner follows the newest signed-in user.
            device.UserId = userId;
            device.Platform = platform;
            device.LastSeenAt = clock.UtcNow;
            device.IsActive = true;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record UnregisterDeviceCommand(string DeviceToken) : IRequest<Result>;

public sealed class UnregisterDeviceHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UnregisterDeviceCommand, Result>
{
    public async Task<Result> Handle(UnregisterDeviceCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var device = await db.Devices.FirstOrDefaultAsync(d => d.DeviceToken == r.DeviceToken && d.UserId == userId, ct);
        if (device is not null)
        {
            device.IsActive = false;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }
}
