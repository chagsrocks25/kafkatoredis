using KafkaToRedis.Domain;

namespace KafkaToRedis.Services;

/// <summary>
/// Represents a single entry in the consumer's in-flight batch.
/// Uses an <see cref="IsTombstone"/> flag instead of a nullable
/// <see cref="PlayerScoreData"/> to avoid nullable reference types in
/// method parameters (RS1008 / nullable-parameter ban).
///
/// Factory methods enforce invariants:
/// <list type="bullet">
///   <item><see cref="ForWrite"/> — regular upsert record.</item>
///   <item><see cref="ForDelete"/> — compaction tombstone (null Kafka value).</item>
/// </list>
/// </summary>
internal readonly record struct BatchRecord
{
    private readonly PlayerScoreData _data;

    /// <summary>Redis key this record targets.</summary>
    public string RedisKey { get; }

    /// <summary>
    /// Payload for upsert. Throws <see cref="InvalidOperationException"/>
    /// if accessed on a tombstone record.
    /// </summary>
    public PlayerScoreData Data =>
        IsTombstone
            ? throw new InvalidOperationException("Cannot access Data on a tombstone BatchRecord.")
            : _data;

    /// <summary><c>true</c> when the Kafka value was null (tombstone — delete from Redis).</summary>
    public bool IsTombstone { get; }

    private BatchRecord(string redisKey, PlayerScoreData data, bool isTombstone)
    {
        RedisKey    = redisKey;
        _data       = data;
        IsTombstone = isTombstone;
    }

    /// <summary>Creates a write (upsert) record.</summary>
    public static BatchRecord ForWrite(string redisKey, PlayerScoreData data) =>
        new(redisKey, data, isTombstone: false);

    /// <summary>Creates a tombstone (delete) record.</summary>
    public static BatchRecord ForDelete(string redisKey) =>
        new(redisKey, default!, isTombstone: true);
}
