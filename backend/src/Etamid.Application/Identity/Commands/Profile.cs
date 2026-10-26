using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Errors;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record UpdateProfileCommand(string? Username, string? Email) : IRequest<Result>;

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.Username!).MaximumLength(150).WithErrorCode("TOO_LONG").When(x => x.Username is not null);
        RuleFor(x => x.Email!).MustBeEmail().When(x => x.Email is not null);
    }
}

public sealed class UpdateProfileHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateProfileCommand, Result>
{
    public async Task<Result> Handle(UpdateProfileCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        if (!string.IsNullOrWhiteSpace(r.Email))
        {
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == email && u.Id != userId && u.DeletedAt == null, ct))
                return IdentityErrors.EmailExists;
        }

        user.UpdateProfile(r.Username, r.Email);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record ChangeLanguageCommand(string Language) : IRequest<Result>;

public sealed class ChangeLanguageValidator : AbstractValidator<ChangeLanguageCommand>
{
    public ChangeLanguageValidator()
        => RuleFor(x => x.Language)
            .Must(v => Enum.TryParse<Etamid.Domain.Identity.Language>(v, true, out _))
            .WithErrorCode("INVALID_VALUE");
}

public sealed class ChangeLanguageHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ChangeLanguageCommand, Result>
{
    public async Task<Result> Handle(ChangeLanguageCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        user.ChangeLanguage(Enum.Parse<Etamid.Domain.Identity.Language>(r.Language, true));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
