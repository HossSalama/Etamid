using System.Globalization;
using Etamid.Application.Common;
using Etamid.Application.Common.Interfaces;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Config;

public sealed record MinVersionDto(string Android, string Ios);

public sealed record PlatformFeeDto(decimal Amount, string PaidBy);

public sealed record AppConfigDto(
    IReadOnlyList<string> Languages,
    MinVersionDto MinVersion,
    bool ForceUpdate,
    string SupportWhatsapp,
    string Currency,
    PlatformFeeDto PlatformFee);

public sealed record LookupItemDto(long Id, string Code, string Name, string? IconUrl);

public sealed record FaqDto(long Id, string Question, string Answer);

// ---------- App config
public sealed record GetAppConfigQuery : IRequest<Result<AppConfigDto>>;

public sealed class GetAppConfigHandler(IApplicationDbContext db) : IRequestHandler<GetAppConfigQuery, Result<AppConfigDto>>
{
    public async Task<Result<AppConfigDto>> Handle(GetAppConfigQuery request, CancellationToken ct)
    {
        var settings = await db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        string Text(string key, string fallback) => settings.TryGetValue(key, out var v) ? v : fallback;

        var fee = decimal.TryParse(Text("platform_fee_amount", "50"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : 50m;

        return new AppConfigDto(
            ["ar", "en"],
            new MinVersionDto(Text("min_version_android", "1.0.0"), Text("min_version_ios", "1.0.0")),
            bool.TryParse(Text("force_update", "false"), out var force) && force,
            Text("support_whatsapp", string.Empty),
            Text("currency", "SAR"),
            new PlatformFeeDto(fee, Text("platform_fee_paid_by", "seller")));
    }
}

// ---------- Lookups
public sealed record GetLookupsQuery(string Type) : IRequest<Result<IReadOnlyList<LookupItemDto>>>;

public sealed class GetLookupsHandler(IApplicationDbContext db, IFileUrlResolver files)
    : IRequestHandler<GetLookupsQuery, Result<IReadOnlyList<LookupItemDto>>>
{
    // API type (kebab, plural) -> value stored in Lookups.Type
    private static readonly Dictionary<string, string> LookupTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["reject-reasons"] = "reject_reason",
        ["dispute-reasons"] = "dispute_reason",
        ["company-reject-reasons"] = "company_reject_reason",
        ["duration-units"] = "duration_unit",
        ["payment-methods"] = "payment_method",
        ["store-categories"] = "store_category"
    };

    public async Task<Result<IReadOnlyList<LookupItemDto>>> Handle(GetLookupsQuery r, CancellationToken ct)
    {
        var type = (r.Type ?? string.Empty).Trim();
        List<(long Id, string Code, string Ar, string En, long? IconId)> rows;

        if (type.Equals("banks", StringComparison.OrdinalIgnoreCase))
        {
            var banks = await db.Banks.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.Id).ToListAsync(ct);
            rows = banks.Select(b => (b.Id, b.Code, b.NameAr, b.NameEn, b.LogoFileId)).ToList();
        }
        else if (type.Equals("shipping-companies", StringComparison.OrdinalIgnoreCase))
        {
            var companies = await db.ShippingCompanies.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(ct);
            rows = companies.Select(c => (c.Id, c.Code, c.NameAr, c.NameEn, c.LogoFileId)).ToList();
        }
        else if (LookupTypes.TryGetValue(type, out var stored))
        {
            var items = await db.Lookups.AsNoTracking()
                .Where(l => l.Type == stored && l.IsActive)
                .OrderBy(l => l.SortOrder)
                .ToListAsync(ct);
            rows = items.Select(l => (l.Id, l.Code, l.NameAr, l.NameEn, l.IconFileId)).ToList();
        }
        else
        {
            return Error.NotFound("LOOKUP_TYPE_NOT_FOUND");
        }

        var iconIds = rows.Where(x => x.IconId.HasValue).Select(x => x.IconId!.Value).Distinct().ToList();
        var iconFiles = iconIds.Count == 0
            ? new Dictionary<long, string?>()
            : (await db.Files.AsNoTracking().Where(f => iconIds.Contains(f.Id)).ToListAsync(ct))
                .ToDictionary(f => f.Id, f => files.Resolve(f));

        IReadOnlyList<LookupItemDto> result = rows
            .Select(x => new LookupItemDto(
                x.Id, x.Code, LanguageHelper.Pick(x.Ar, x.En),
                x.IconId.HasValue && iconFiles.TryGetValue(x.IconId.Value, out var url) ? url : null))
            .ToList();

        return Result.Success(result);
    }
}

// ---------- FAQs
public sealed record GetFaqsQuery : IRequest<Result<IReadOnlyList<FaqDto>>>;

public sealed class GetFaqsHandler(IApplicationDbContext db) : IRequestHandler<GetFaqsQuery, Result<IReadOnlyList<FaqDto>>>
{
    public async Task<Result<IReadOnlyList<FaqDto>>> Handle(GetFaqsQuery request, CancellationToken ct)
    {
        var faqs = await db.Faqs.AsNoTracking().Where(f => f.IsActive).OrderBy(f => f.SortOrder).ToListAsync(ct);

        IReadOnlyList<FaqDto> result = faqs
            .Select(f => new FaqDto(f.Id, LanguageHelper.Pick(f.QuestionAr, f.QuestionEn), LanguageHelper.Pick(f.AnswerAr, f.AnswerEn)))
            .ToList();

        return Result.Success(result);
    }
}
