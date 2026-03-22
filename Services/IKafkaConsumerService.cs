namespace KafkaToRedis.Services;

/// <summary>
/// Defines the lifecycle contract for the Kafka consumer background service.
/// </summary>
public interface IKafkaConsumerService
{
    /// <summary>
    /// Starts the poll-deserialise-flush loop and runs until
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task ConsumeAsync(CancellationToken cancellationToken);
}
