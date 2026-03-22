using StackExchange.Redis;

namespace KafkaToRedis.Mapping;

/// <summary>
/// Strategy contract for converting a domain object into a set of Redis hash
/// field/value pairs.
///
/// Implement this interface to change the hash schema — e.g. add new fields,
/// rename fields, or store values in a different format — without modifying the
/// repository or consumer.
/// </summary>
/// <typeparam name="TValue">The domain model to convert from.</typeparam>
public interface IRedisHashMapper<in TValue>
    where TValue : class
{
    /// <summary>
    /// Produces the <see cref="HashEntry"/> array that will be written via
    /// <c>HSET</c> to Redis.
    /// </summary>
    HashEntry[] Map(TValue value);
}
