using System.Text.Json;
using Confluent.Kafka;
using KafkaToRedis.Configuration;
using KafkaToRedis.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KafkaToRedis.Services;

/// <summary>
/// Produces a structured test scenario to Kafka so the full pipeline can be
/// exercised end-to-end from a single command (<c>dotnet run -- --produce</c>).
///
/// Test scenario
/// ─────────────
/// The scenario deliberately includes:
/// <list type="number">
///   <item>Initial writes for three distinct players across different platforms</item>
///   <item>A second <c>wins</c> score for player 1 — stored under a different hash-field prefix</item>
///   <item>An <b>update</b> of player 1's <c>playtime</c> score (normalized_value 0.45 → 0.91)</item>
/// </list>
///
/// Expected Redis state after the consumer processes all messages:
/// <code>
///   sbmm:987654321:3  → playtime:normalized_value = 0.91  (UPDATED from 0.45)
///                        wins:normalized_value     = 0.72
///   sbmm:123456789:5  → playtime:normalized_value = 0.88
///   sbmm:555000111:7  → playtime:normalized_value = 0.33
/// </code>
/// </summary>
public sealed class KafkaTestDataProducer : ITestDataProducer, IDisposable
{
    // Default options; [JsonPropertyName] attributes on the model drive field naming.
    private static readonly JsonSerializerOptions _serializerOptions = new();

    private readonly IProducer<string, string>     _producer;
    private readonly string                         _topic;
    private readonly ILogger<KafkaTestDataProducer> _logger;

    public KafkaTestDataProducer(
        IOptions<KafkaOptions>          kafkaOptions,
        ILogger<KafkaTestDataProducer>  logger)
    {
        var options = kafkaOptions.Value;
        _topic      = options.Topic;
        _logger     = logger;

        // The producer is owned by this class; we do not expose IProducer<,> to DI
        // because it is only used in test/produce mode.
        _producer = new ProducerBuilder<string, string>(
            new ProducerConfig { BootstrapServers = options.BootstrapServers })
            .Build();
    }

    /// <inheritdoc/>
    public async Task ProduceAsync(CancellationToken cancellationToken = default)
    {
        var scenario = BuildScenario();
        int count    = 0;

        _logger.LogInformation(
            "Producing {Total} test message(s) to topic '{Topic}'…",
            scenario.Count, _topic);

        foreach (var (kafkaKey, data, label) in scenario)
        {
            var json    = JsonSerializer.Serialize(data, _serializerOptions);
            var message = new Message<string, string> { Key = kafkaKey, Value = json };

            var result = await _producer.ProduceAsync(_topic, message, cancellationToken);
            count++;

            _logger.LogInformation(
                "  [{Label,-7}] partition={P} offset={O} | "
                + "key={Key,-15} scoreId={Score,-10} normalized={Norm}",
                label,
                result.Partition.Value,
                result.Offset.Value,
                kafkaKey,
                data.ScoreId,
                data.NormalizedValue);
        }

        _producer.Flush(cancellationToken);

        _logger.LogInformation(
            "Done — {Count} message(s) produced. "
            + "Now run 'dotnet run' and press Ctrl-C after a few seconds, "
            + "then run 'make verify' to inspect Redis.",
            count);
    }

    // -------------------------------------------------------------------------
    // Test scenario definition
    // -------------------------------------------------------------------------

    private static IReadOnlyList<(string KafkaKey, PlayerScoreData Data, string Label)> BuildScenario()
    {
        var now = DateTime.UtcNow;
        var ttl = now.AddYears(1);

        return
        [
            // ── Initial writes ──────────────────────────────────────────────
            ("3_987654321",
             new PlayerScoreData
             {
                 PlatformAccountId = new("3_987654321"),
                 ScoreId           = "playtime",
                 Created           = now,
                 Ttl               = ttl,
                 Version           = "1.0.0",
                 RawValue          = 45_000,
                 NormalizedValue   = 0.45m      // will be overwritten by the UPDATE below
             },
             "INITIAL"),

            ("3_987654321",
             new PlayerScoreData
             {
                 PlatformAccountId = new("3_987654321"),
                 ScoreId           = "wins",    // different scoreId → separate hash fields
                 Created           = now,
                 Ttl               = ttl,
                 Version           = "1.0.0",
                 RawValue          = 72_000,
                 NormalizedValue   = 0.72m
             },
             "INITIAL"),

            ("5_123456789",
             new PlayerScoreData
             {
                 PlatformAccountId = new("5_123456789"),
                 ScoreId           = "playtime",
                 Created           = now,
                 Ttl               = ttl,
                 Version           = "1.0.0",
                 RawValue          = 88_000,
                 NormalizedValue   = 0.88m
             },
             "INITIAL"),

            ("7_555000111",
             new PlayerScoreData
             {
                 PlatformAccountId = new("7_555000111"),
                 ScoreId           = "playtime",
                 Created           = now,
                 Ttl               = ttl,
                 Version           = "1.0.0",
                 RawValue          = 33_000,
                 NormalizedValue   = 0.33m
             },
             "INITIAL"),

            // ── Update (same Kafka key + same scoreId → last-write-wins) ───
            // Redis must show 0.91, not 0.45
            ("3_987654321",
             new PlayerScoreData
             {
                 PlatformAccountId = new("3_987654321"),
                 ScoreId           = "playtime",
                 Created           = now,
                 Ttl               = ttl,
                 Version           = "1.1.0",   // bumped version
                 RawValue          = 91_000,
                 NormalizedValue   = 0.91m      // ← updated value
             },
             "UPDATE"),
        ];
    }

    public void Dispose() => _producer?.Dispose();
}
