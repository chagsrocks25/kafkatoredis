namespace KafkaToRedis.Services;

/// <summary>
/// Produces a predefined test scenario to the Kafka topic so the pipeline
/// can be exercised end-to-end without an external data source.
///
/// The scenario includes:
/// <list type="bullet">
///   <item>Initial writes for several (platformId, accountId, scoreId) combinations</item>
///   <item>An update for one player's score to verify last-write-wins semantics in Redis</item>
/// </list>
/// </summary>
public interface ITestDataProducer
{
    Task ProduceAsync(CancellationToken cancellationToken = default);
}
