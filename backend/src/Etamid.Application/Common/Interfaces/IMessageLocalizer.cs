namespace Etamid.Application.Common.Interfaces;

/// <summary>Resolves a localized message by its key (usually an error code) for the current request language.</summary>
public interface IMessageLocalizer
{
    string Get(string key);
}
