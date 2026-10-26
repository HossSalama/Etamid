using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Identity;
using Etamid.Domain.Identity.ValueObjects;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record AddressDto(
    long Id, string? Label, string RecipientName, string RecipientPhone, string City, string? District, string? Street,
    string? BuildingNo, string? PostalCode, string? AdditionalNo, string FullText, decimal? Latitude, decimal? Longitude, bool IsDefault);

internal static class AddressMapper
{
    public const int MaxPerUser = 10;

    public static AddressDto ToDto(Address a)
    {
        var phone = PhoneNumber.TryParse(a.RecipientPhone, out var p) ? p!.Local : a.RecipientPhone;
        return new AddressDto(a.Id, a.Label, a.RecipientName, phone, a.City, a.District, a.Street, a.BuildingNo,
            a.PostalCode, a.AdditionalNo, a.FullText, a.Latitude, a.Longitude, a.IsDefault);
    }

    public static string FullText(string city, string? district, string? street, string? buildingNo, string? postalCode)
    {
        var line = string.Join(" ", new[] { buildingNo, street }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var parts = new[] { line, district, city, postalCode }.Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join("، ", parts!.Select(s => s!.Trim()));
    }

    public static void Fill(Address a, string? Label, string RecipientName, string RecipientPhone, string City, string? District, string? Street, string? BuildingNo, string? PostalCode, string? AdditionalNo, decimal? Latitude, decimal? Longitude)
    {
        a.Label = Clean(Label);
        a.RecipientName = RecipientName.Trim();
        a.RecipientPhone = PhoneNumber.TryParse(RecipientPhone, out var phone) ? phone!.E164 : RecipientPhone;
        a.City = City.Trim();
        a.District = Clean(District);
        a.Street = Clean(Street);
        a.BuildingNo = Clean(BuildingNo);
        a.PostalCode = Clean(PostalCode);
        a.AdditionalNo = Clean(AdditionalNo);
        a.Latitude = Latitude;
        a.Longitude = Longitude;
        a.FullText = FullText(a.City, a.District, a.Street, a.BuildingNo, a.PostalCode);
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

// ---------- List
public sealed record ListMyAddressesQuery : IRequest<Result<IReadOnlyList<AddressDto>>>;

public sealed class ListMyAddressesHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListMyAddressesQuery, Result<IReadOnlyList<AddressDto>>>
{
    public async Task<Result<IReadOnlyList<AddressDto>>> Handle(ListMyAddressesQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var items = await db.Addresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        IReadOnlyList<AddressDto> result = items.Select(AddressMapper.ToDto).ToList();
        return Result.Success(result);
    }
}

// ---------- Add
public sealed record AddAddressCommand(string? Label, string RecipientName, string RecipientPhone, string City, string? District, string? Street, string? BuildingNo, string? PostalCode, string? AdditionalNo, decimal? Latitude, decimal? Longitude) : IRequest<Result<AddressDto>>;

public sealed class AddAddressValidator : AbstractValidator<AddAddressCommand>
{
    public AddAddressValidator()
    {
        RuleFor(x => x.Label!).MaximumLength(50).WithErrorCode("TOO_LONG").When(x => x.Label is not null);
        RuleFor(x => x.RecipientName).MustBeRequired(150);
        RuleFor(x => x.RecipientPhone).MustBeSaudiPhone();
        RuleFor(x => x.City).MustBeRequired(80);
        RuleFor(x => x.District!).MaximumLength(80).WithErrorCode("TOO_LONG").When(x => x.District is not null);
        RuleFor(x => x.Street!).MaximumLength(150).WithErrorCode("TOO_LONG").When(x => x.Street is not null);
        RuleFor(x => x.BuildingNo!).MaximumLength(20).WithErrorCode("TOO_LONG").When(x => x.BuildingNo is not null);
        RuleFor(x => x.PostalCode!).MaximumLength(10).WithErrorCode("TOO_LONG").When(x => x.PostalCode is not null);
        RuleFor(x => x.AdditionalNo!).MaximumLength(10).WithErrorCode("TOO_LONG").When(x => x.AdditionalNo is not null);
        RuleFor(x => x.Latitude!.Value).InclusiveBetween(-90m, 90m).WithErrorCode("INVALID_VALUE").When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude!.Value).InclusiveBetween(-180m, 180m).WithErrorCode("INVALID_VALUE").When(x => x.Longitude.HasValue);
    }
}

public sealed class AddAddressHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<AddAddressCommand, Result<AddressDto>>
{
    public async Task<Result<AddressDto>> Handle(AddAddressCommand c, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var existing = await db.Addresses.Where(a => a.UserId == userId).CountAsync(ct);
        if (existing >= AddressMapper.MaxPerUser) return Error.Validation("ADDRESS_LIMIT_REACHED");

        var address = new Address { UserId = userId, IsDefault = existing == 0 };
        AddressMapper.Fill(address, c.Label, c.RecipientName, c.RecipientPhone, c.City, c.District, c.Street, c.BuildingNo, c.PostalCode, c.AdditionalNo, c.Latitude, c.Longitude);
        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);

        return AddressMapper.ToDto(address);
    }
}

// ---------- Update (Id comes from the route)
public sealed record UpdateAddressCommand(long Id, string? Label, string RecipientName, string RecipientPhone, string City, string? District, string? Street, string? BuildingNo, string? PostalCode, string? AdditionalNo, decimal? Latitude, decimal? Longitude) : IRequest<Result<AddressDto>>;

public sealed class UpdateAddressValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressValidator()
    {
        RuleFor(x => x.Label!).MaximumLength(50).WithErrorCode("TOO_LONG").When(x => x.Label is not null);
        RuleFor(x => x.RecipientName).MustBeRequired(150);
        RuleFor(x => x.RecipientPhone).MustBeSaudiPhone();
        RuleFor(x => x.City).MustBeRequired(80);
        RuleFor(x => x.District!).MaximumLength(80).WithErrorCode("TOO_LONG").When(x => x.District is not null);
        RuleFor(x => x.Street!).MaximumLength(150).WithErrorCode("TOO_LONG").When(x => x.Street is not null);
        RuleFor(x => x.BuildingNo!).MaximumLength(20).WithErrorCode("TOO_LONG").When(x => x.BuildingNo is not null);
        RuleFor(x => x.PostalCode!).MaximumLength(10).WithErrorCode("TOO_LONG").When(x => x.PostalCode is not null);
        RuleFor(x => x.AdditionalNo!).MaximumLength(10).WithErrorCode("TOO_LONG").When(x => x.AdditionalNo is not null);
        RuleFor(x => x.Latitude!.Value).InclusiveBetween(-90m, 90m).WithErrorCode("INVALID_VALUE").When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude!.Value).InclusiveBetween(-180m, 180m).WithErrorCode("INVALID_VALUE").When(x => x.Longitude.HasValue);
    }
}

public sealed class UpdateAddressHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateAddressCommand, Result<AddressDto>>
{
    public async Task<Result<AddressDto>> Handle(UpdateAddressCommand c, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var address = await db.Addresses.FirstOrDefaultAsync(a => a.Id == c.Id && a.UserId == userId, ct);
        if (address is null) return Error.NotFound("ADDRESS_NOT_FOUND");

        AddressMapper.Fill(address, c.Label, c.RecipientName, c.RecipientPhone, c.City, c.District, c.Street, c.BuildingNo, c.PostalCode, c.AdditionalNo, c.Latitude, c.Longitude);
        await db.SaveChangesAsync(ct);

        return AddressMapper.ToDto(address);
    }
}

// ---------- Set default
public sealed record SetDefaultAddressCommand(long Id) : IRequest<Result>;

public sealed class SetDefaultAddressHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<SetDefaultAddressCommand, Result>
{
    public async Task<Result> Handle(SetDefaultAddressCommand c, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var all = await db.Addresses.Where(a => a.UserId == userId).ToListAsync(ct);
        var target = all.FirstOrDefault(a => a.Id == c.Id);
        if (target is null) return Error.NotFound("ADDRESS_NOT_FOUND");

        foreach (var a in all) a.IsDefault = a.Id == target.Id;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------- Delete (soft)
public sealed record DeleteAddressCommand(long Id) : IRequest<Result>;

public sealed class DeleteAddressHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<DeleteAddressCommand, Result>
{
    public async Task<Result> Handle(DeleteAddressCommand c, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var all = await db.Addresses.Where(a => a.UserId == userId).ToListAsync(ct);
        var target = all.FirstOrDefault(a => a.Id == c.Id);
        if (target is null) return Error.NotFound("ADDRESS_NOT_FOUND");

        target.DeletedAt = clock.UtcNow;
        if (target.IsDefault)
        {
            target.IsDefault = false;
            var next = all.Where(a => a.Id != target.Id).OrderByDescending(a => a.Id).FirstOrDefault();
            if (next is not null) next.IsDefault = true;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
