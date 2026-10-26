using System.Text.Json;
using Etamid.Application.Common;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Dtos;
using Etamid.Application.Identity.Errors;
using Etamid.Application.Identity.Services;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Queries;

public sealed record GetMyProfileQuery : IRequest<Result<UserDto>>;

public sealed class GetMyProfileHandler(IApplicationDbContext db, ICurrentUser currentUser, UserProfileService profiles)
    : IRequestHandler<GetMyProfileQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetMyProfileQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        return await profiles.BuildAsync(user, ct);
    }
}

public sealed record GetTermsQuery(string Type) : IRequest<Result<TermsDto>>;

public sealed class GetTermsHandler(IApplicationDbContext db) : IRequestHandler<GetTermsQuery, Result<TermsDto>>
{
    public async Task<Result<TermsDto>> Handle(GetTermsQuery r, CancellationToken ct)
    {
        if (!Enum.TryParse<TermsType>(r.Type, true, out var type))
            return IdentityErrors.InvalidTermsType;

        var terms = await db.TermsVersions.AsNoTracking()
            .Where(t => t.Type == type && t.IsActive)
            .OrderByDescending(t => t.PublishedAt)
            .FirstOrDefaultAsync(ct);
        if (terms is null) return IdentityErrors.TermsNotFound;

        var items = JsonSerializer.Deserialize<string[]>(LanguageHelper.IsArabic ? terms.ItemsAr : terms.ItemsEn) ?? [];
        return new TermsDto(terms.Version, LanguageHelper.Pick(terms.TitleAr, terms.TitleEn), items);
    }
}
