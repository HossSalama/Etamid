using System.Globalization;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;

namespace Etamid.Api.Localization;

/// <summary>Loads Resources/messages.{lang}.json once and resolves messages by key for the request culture.</summary>
public sealed class JsonMessageLocalizer : IMessageLocalizer
{
    private readonly Dictionary<string, Dictionary<string, string>> _messages = new(StringComparer.OrdinalIgnoreCase);

    public JsonMessageLocalizer()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Resources");
        foreach (var lang in new[] { "en", "ar" })
        {
            var path = Path.Combine(dir, $"messages.{lang}.json");
            _messages[lang] = File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? []
                : [];
        }
    }

    public string Get(string key)
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (_messages.TryGetValue(lang, out var table) && table.TryGetValue(key, out var value))
            return value;
        if (_messages["en"].TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }
}
