namespace KafkaToRedis.Mapping;

/// <summary>
/// Strategy contract for translating a raw Kafka key string into a Redis key string.
///
/// Implement this interface to change the Redis keyspace scheme without modifying
/// the consumer or repository — e.g. add a tenant prefix, change the separator, or
/// support a different key format entirely.
/// </summary>
public interface IRedisKeyMapper
{
    /// <summary>
    /// Attempts to map <paramref name="kafkaKey"/> to a Redis key.
    /// Returns <c>true</c> and sets <paramref name="redisKey"/> on success;
    /// returns <c>false</c> when the key is invalid or cannot be mapped.
    /// </summary>
    bool TryMap(string kafkaKey, out string redisKey);
}
