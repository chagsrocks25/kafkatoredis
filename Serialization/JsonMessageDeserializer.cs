using System.Text.Json;
using KafkaToRedis.Domain;
using Microsoft.Extensions.Logging;

namespace KafkaToRedis.Serialization;

/// <summary>
/// Deserialises Kafka message values from JSON using <see cref="System.Text.Json"/>.
/// </summary>
public sealed class JsonMessageDeserializer : IMessageDeserializer<PlayerScoreData>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<JsonMessageDeserializer> _logger;

    public JsonMessageDeserializer(ILogger<JsonMessageDeserializer> logger) =>
        _logger = logger;

    /// <inheritdoc/>
    public bool TryDeserialize(string rawPayload, out PlayerScoreData result)
    {
        try
        {
            var deserialized = JsonSerializer.Deserialize<PlayerScoreData>(rawPayload, JsonOptions);
            if (deserialized is not null)
            {
                result = deserialized;
                return true;
            }

            _logger.LogWarning("JSON deserialization produced null for payload.");
            result = default!;
            return false;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("JSON deserialization failed: {Message}", ex.Message);
            result = default!;
            return false;
        }
    }
}
