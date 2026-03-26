namespace KafkaToRedis.Configuration;

/// <summary>
/// Strongly-typed options for the Redis connection.
/// Bound from the "Redis" configuration section.
/// Override with environment variable <c>Redis__ConnectionString</c>.
/// </summary>
public sealed class RedisOptions
{
    public string ConnectionString { get; init; } = "localhost:6379";

    /// <summary>
    /// Optional connection timeout in milliseconds. If not provided, StackExchange.Redis defaults apply.
    /// Override with <c>Redis__ConnectTimeoutMs</c>.
    /// </summary>
    public int? ConnectTimeoutMs { get; init; }
}
