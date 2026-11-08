namespace Etamid.Application.Common.Interfaces;

/// <summary>Resolves a localized message by its key (usually an error code) for the current request language.</summary>
public interface IMessageLocalizer
{
    string Get(string key);

    /// <summary>Resolves a message by its key in the given language ("ar" or "en"), falling back to English.</summary>
    string Get(string key, string language);
}
