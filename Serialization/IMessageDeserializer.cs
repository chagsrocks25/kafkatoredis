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
    /// Attempts to deserialise <paramref name="rawPayload"/> into <typeparamref name="TDomain"/>.
    /// Returns <c>true</c> and sets <paramref name="result"/> on success;
    /// returns <c>false</c> on invalid/unparseable payloads (implementations should log
    /// a warning and allow the caller to skip the record).
    /// </summary>
    bool TryDeserialize(string rawPayload, out TDomain result);
}
