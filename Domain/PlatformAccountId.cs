using System.Text.Json;
using System.Text.Json.Serialization;

namespace KafkaToRedis.Domain;

/// <summary>
/// Value object that encapsulates the Kafka key <c>{platformId}_{accountId}</c>.
/// Splits on the first underscore and parses each segment into its correct type:
/// <list type="bullet">
///   <item><c>PlatformId</c> — <see cref="int"/>, ranges 1–10</item>
///   <item><c>AccountId</c>  — <see cref="long"/>, a large numeric account identifier</item>
/// </list>
/// </summary>
public sealed class PlatformAccountId
{
    public string OriginalValue { get; }

    /// <summary>Platform identifier (1–10).</summary>
    public int  PlatformId { get; }

    /// <summary>Account identifier (long).</summary>
    public long AccountId  { get; }

    /// <summary>
    /// <c>true</c> when the original value was successfully parsed into a valid
    /// <see cref="int"/> platform ID and <see cref="long"/> account ID.
    /// </summary>
    public bool IsValid { get; }

    public PlatformAccountId(string value)
    {
        OriginalValue = value ?? string.Empty;

        var separatorIndex = OriginalValue.IndexOf('_');
        if (separatorIndex > 0 && separatorIndex < OriginalValue.Length - 1)
        {
            var platformSegment = OriginalValue[..separatorIndex];
            var accountSegment  = OriginalValue[(separatorIndex + 1)..];

            if (int.TryParse(platformSegment,  out var platformId) &&
                long.TryParse(accountSegment,   out var accountId))
            {
                PlatformId = platformId;
                AccountId  = accountId;
                IsValid    = true;
            }
        }
    }

    public override string ToString() => OriginalValue;
}

/// <summary>
/// Custom <see cref="JsonConverter{T}"/> that deserialises a raw string into
/// a <see cref="PlatformAccountId"/> value object, and serialises it back to
/// its original string form.
/// </summary>
public sealed class PlatformAccountIdConverter : JsonConverter<PlatformAccountId>
{
    public override PlatformAccountId Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? string.Empty);

    public override void Write(
        Utf8JsonWriter writer, PlatformAccountId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.OriginalValue);
}
