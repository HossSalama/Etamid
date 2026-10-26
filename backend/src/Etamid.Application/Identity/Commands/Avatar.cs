using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Files;
using Etamid.Application.Identity.Errors;
using Etamid.Domain.Files;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record AvatarDto(string AvatarUrl);

public sealed record UploadAvatarCommand(Stream Content, string? FileName) : IRequest<Result<AvatarDto>>;

public sealed class UploadAvatarHandler(FileUploadService uploads, IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver urls)
    : IRequestHandler<UploadAvatarCommand, Result<AvatarDto>>
{
    public async Task<Result<AvatarDto>> Handle(UploadAvatarCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        var stored = await uploads.StoreAsync(r.Content, userId, FilePurpose.Avatar, r.FileName, imagesOnly: true, ct);
        if (stored.IsFailure) return stored.Error;

        var file = stored.Value;
        await db.SaveChangesAsync(ct);          // assigns file.Id

        file.Status = FileStatus.Attached;
        user.SetAvatar(file.Id);
        await db.SaveChangesAsync(ct);

        return new AvatarDto(urls.Resolve(file)!);
    }
}
