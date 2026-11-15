using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Etamid.Api.Extensions;

/// <summary>
/// All timestamps in the API are UTC: the domain clock is UTC, and SQL Server
/// datetime2 carries no offset, so values read back from the database have
/// Kind Unspecified. Without these converters System.Text.Json writes them
/// with no offset ("2026-11-15T09:14:00") and browsers parse that as LOCAL
/// time — 2h behind Cairo. These converters mark every DateTime as UTC on the
/// way out (always with 'Z') and treat a value with no offset as UTC on the
/// way in. DateTimeOffset is untouched.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (text is null) throw new JsonException("Expected a date-time string.");
        return ParseAsUtc(text);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToUtc(value));

    internal static DateTime ToUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    internal static DateTime ParseAsUtc(string text)
    {
        // AssumeUniversal: no offset means UTC. AdjustToUniversal: an offset is converted to UTC.
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        throw new JsonException($"'{text}' is not a valid date-time.");
    }
}

public sealed class UtcNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : UtcDateTimeJsonConverter.ParseAsUtc(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(UtcDateTimeJsonConverter.ToUtc(value.Value));
    }
}
