using KafkaToRedis.Domain;

namespace KafkaToRedis.Mapping;

/// <summary>
/// Maps the <c>{platformId}_{accountId}</c> Kafka key to the Redis keyspace
/// <c>sbmm:{accountId}:{platformId}</c>.
///
/// Hash fields within that key are further namespaced by <c>scoreId</c> (e.g.
/// <c>playtime:normalized_value</c>), allowing a single hash to hold all score
/// types for one player account.
/// </summary>
public sealed class PlatformAccountKeyMapper : IRedisKeyMapper
{
    /// <inheritdoc/>
    public string? Map(string kafkaKey)
    {
        var id = new PlatformAccountId(kafkaKey);
        return id.IsValid
            ? $"sbmm:{id.AccountId}:{id.PlatformId}"
            : null;
    }
}
