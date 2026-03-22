using System.Text.Json;
using KafkaToRedis.Domain;
using Microsoft.Extensions.Logging;

namespace KafkaToRedis.Serialization;

/// <summary>
/// Deserialises Kafka message values from JSON using <see cref="System.Text.Json"/>.
/// </summary>
public sealed class JsonMessageDeserializer : IMessageDeserializer<PlayerScoreData>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<JsonMessageDeserializer> _logger;

    public JsonMessageDeserializer(ILogger<JsonMessageDeserializer> logger) =>
        _logger = logger;

    /// <inheritdoc/>
    public PlayerScoreData? Deserialize(string? rawPayload)
    {
        if (rawPayload is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<PlayerScoreData>(rawPayload, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("JSON deserialization failed: {Message}", ex.Message);
            return null;
        }
    }
}
