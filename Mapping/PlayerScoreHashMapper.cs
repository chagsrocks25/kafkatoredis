using KafkaToRedis.Domain;
using StackExchange.Redis;

namespace KafkaToRedis.Mapping;

/// <summary>
/// Maps a <see cref="PlayerScoreData"/> to Redis hash entries, prefixing every
/// field with the record's <c>ScoreId</c>.
///
/// This co-locates all score types for one player in a single Redis hash and
/// allows direct O(1) field access, e.g.:
/// <code>
///   HGET sbmm:{accountId}:{platformId} playtime:normalized_value
/// </code>
/// </summary>
public sealed class PlayerScoreHashMapper : IRedisHashMapper<PlayerScoreData>
{
    /// <inheritdoc/>
    public HashEntry[] Map(PlayerScoreData data)
    {
        if (string.IsNullOrEmpty(data.ScoreId))
            throw new ArgumentException(
                "ScoreId must not be null or empty — it is used as the Redis hash field prefix.",
                nameof(data));

        var scorePrefix = data.ScoreId; // field prefix, e.g. "playtime"

        return
        [
            new($"{scorePrefix}:platform_account_id", data.PlatformAccountId.OriginalValue),
            new($"{scorePrefix}:score_id",            data.ScoreId),
            new($"{scorePrefix}:created",             data.Created.ToString("O")),
            new($"{scorePrefix}:ttl",                 data.Ttl.ToString("O")),
            new($"{scorePrefix}:version",             data.Version ?? string.Empty),
            new($"{scorePrefix}:raw_value",           data.RawValue),
            new($"{scorePrefix}:normalized_value",    (double)data.NormalizedValue)
        ];
    }
}
