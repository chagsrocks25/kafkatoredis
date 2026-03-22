using KafkaToRedis.Domain;

namespace KafkaToRedis.Repositories;

/// <summary>
/// Repository abstraction for persisting player score data.
///
/// Implementations must honour last-write-wins ordering: when the consumer calls
/// <see cref="WriteAsync"/> for the same key twice in sequence, the second call
/// must be the value visible to readers afterwards.
///
/// Swap this implementation to target a different store (DynamoDB, Cassandra, etc.)
/// without changing any other code.
/// </summary>
public interface IScoreRepository
{
    /// <summary>
    /// Upserts the score data for the given Redis key.
    /// </summary>
    Task WriteAsync(
        string          redisKey,
        PlayerScoreData data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the entire key (tombstone handling for compacted topics).
    /// </summary>
    Task DeleteAsync(
        string redisKey,
        CancellationToken cancellationToken = default);
}
