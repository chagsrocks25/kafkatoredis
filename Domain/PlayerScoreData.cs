using System.Text.Json.Serialization;

namespace KafkaToRedis.Domain;

/// <summary>
/// Domain model for the JSON payload consumed from the Kafka topic.
/// </summary>
public sealed class PlayerScoreData
{
    /// <summary>
    /// Combined platform+account key, e.g. <c>3_987654321</c>.
    /// Parsed by <see cref="PlatformAccountIdConverter"/>.
    /// </summary>
    [JsonPropertyName("platform_account_id")]
    [JsonConverter(typeof(PlatformAccountIdConverter))]
    public PlatformAccountId PlatformAccountId { get; init; } = new(string.Empty);

    /// <summary>
    /// Logical score identifier, e.g. <c>"playtime"</c>, <c>"wins"</c>.
    /// Used as a hash-field prefix in Redis so a single hash holds all score types
    /// for one player.
    /// </summary>
    [JsonPropertyName("score_id")]
    public string ScoreId { get; init; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTime Created { get; init; }

    [JsonPropertyName("ttl")]
    public DateTime Ttl { get; init; }

    /// <summary>Semantic version string, e.g. <c>"1.2.3"</c>.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("raw_value")]
    public long RawValue { get; init; }

    /// <summary>Normalised score in the range [0, 1].</summary>
    [JsonPropertyName("normalized_value")]
    public decimal NormalizedValue { get; init; }
}
