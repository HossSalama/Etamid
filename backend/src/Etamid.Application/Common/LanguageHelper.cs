using System.Globalization;
using Etamid.Domain.Identity;

namespace Etamid.Application.Common;

public static class LanguageHelper
{
    public static bool IsArabic => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    public static Language Current => IsArabic ? Language.Ar : Language.En;

    public static string Pick(string ar, string en) => IsArabic ? ar : en;
}
