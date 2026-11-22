using Etamid.Application.Common;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Config;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Dashboard;

/// <summary>
/// Everything the guest home screen shows, in one call: the wallet state
/// (always not activated for a guest — that is the whole point of the page),
/// the support WhatsApp number and the public FAQs. No user, no deals, no
/// balances — a guest has none of these. No hero copy: the static brand text
/// lives in the iOS app.
/// </summary>
public sealed record GuestHomeDto(
    GuestWalletDto Wallet,
    string SupportWhatsapp,
    IReadOnlyList<FaqDto> Faqs);

public sealed record GuestWalletDto(bool Activated);

public sealed record GetGuestHomeQuery : IRequest<Result<GuestHomeDto>>;

public sealed class GetGuestHomeHandler(IApplicationDbContext db)
    : IRequestHandler<GetGuestHomeQuery, Result<GuestHomeDto>>
{
    public async Task<Result<GuestHomeDto>> Handle(GetGuestHomeQuery request, CancellationToken ct)
    {
        // Same AppSettings read as GetAppConfigHandler: empty-string fallback.
        var settings = await db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var supportWhatsapp = settings.TryGetValue("support_whatsapp", out var v) ? v : string.Empty;

        // Same list as GetFaqsQuery, exactly: active FAQs ordered by SortOrder,
        // localized via LanguageHelper.Pick. (No handler-to-handler call: no
        // Application handler sends another query through ISender today.)
        var faqs = await db.Faqs.AsNoTracking().Where(f => f.IsActive).OrderBy(f => f.SortOrder).ToListAsync(ct);

        IReadOnlyList<FaqDto> items = faqs
            .Select(f => new FaqDto(f.Id, LanguageHelper.Pick(f.QuestionAr, f.QuestionEn), LanguageHelper.Pick(f.AnswerAr, f.AnswerEn)))
            .ToList();

        return Result.Success(new GuestHomeDto(new GuestWalletDto(Activated: false), supportWhatsapp, items));
    }
}
