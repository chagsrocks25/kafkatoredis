namespace KafkaToRedis.Configuration;

/// <summary>
/// Strongly-typed options for the Kafka consumer.
/// Bound from the "Kafka" configuration section.
/// All values can be overridden with environment variables using double-underscore
/// notation, e.g. <c>Kafka__BootstrapServers=broker:9092</c>.
/// </summary>
public sealed class KafkaOptions
{
    public string BootstrapServers     { get; init; } = "localhost:9092";
    public string Topic                { get; init; } = "scores";
    public string GroupId              { get; init; } = "kafka-to-redis-consumer";

    /// <summary>Maximum records to accumulate before forcing a Redis flush.</summary>
    public int MaxBatchSize            { get; init; } = 1000;

    /// <summary>Maximum seconds to wait before flushing a non-empty batch.</summary>
    public int MaxBatchDelaySeconds    { get; init; } = 2;
}
