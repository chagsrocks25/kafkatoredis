namespace KafkaToRedis.Serialization;

/// <summary>
/// Strategy contract for deserialising a raw Kafka message value string into a
/// typed domain object.
///
/// Implement this interface to swap serialisation formats without touching the
/// consumer pipeline — e.g. replace JSON with Avro or Protobuf.
/// </summary>
/// <typeparam name="TDomain">The domain model to produce.</typeparam>
public interface IMessageDeserializer<TDomain>
    where TDomain : class
{
    /// <summary>
    /// Deserialises <paramref name="rawPayload"/> into <typeparamref name="TDomain"/>.
    /// Returns <c>null</c> when the payload cannot be deserialised (implementations
    /// should log a warning and allow the caller to skip the record).
    /// </summary>
    TDomain? Deserialize(string? rawPayload);
}
