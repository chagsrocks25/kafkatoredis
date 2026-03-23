using Confluent.Kafka;
using KafkaToRedis.Configuration;
using KafkaToRedis.Domain;
using KafkaToRedis.Mapping;
using KafkaToRedis.Repositories;
using KafkaToRedis.Serialization;
using KafkaToRedis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KafkaToRedis.Infrastructure;

/// <summary>
/// DI composition root for the KafkaToRedis module.
///
/// Call <see cref="AddKafkaToRedis"/> from the application entry point after
/// registering <c>IConnectionMultiplexer</c> (which requires async initialisation).
///
/// To extend the pipeline without modifying existing classes:
/// <list type="bullet">
///   <item>Replace serialisation: register a different <c>IMessageDeserializer&lt;PlayerScoreData&gt;</c></item>
///   <item>Change key scheme: register a different <c>IRedisKeyMapper</c></item>
///   <item>Change hash layout: register a different <c>IRedisHashMapper&lt;PlayerScoreData&gt;</c></item>
///   <item>Change store: register a different <c>IScoreRepository</c></item>
///   <item>Add cross-cutting concerns: stack another <c>IScoreRepository</c> decorator</item>
/// </list>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all KafkaToRedis services.
    /// Expects <c>IConnectionMultiplexer</c> to already be registered in
    /// <paramref name="services"/> (Redis requires async connection setup).
    /// </summary>
    public static IServiceCollection AddKafkaToRedis(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // --- Options ----------------------------------------------------------
        services
            .Configure<KafkaOptions>(configuration.GetSection("Kafka"))
            .Configure<RedisOptions>(configuration.GetSection("Redis"));

        // --- Kafka consumer ---------------------------------------------------
        // Registered as singleton; Confluent's IConsumer is not thread-safe but
        // is only ever accessed from the single consumer loop.
        services.AddSingleton<IConsumer<string, string>>(sp =>
        {
            var kafkaOptions = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = kafkaOptions.BootstrapServers,
                GroupId          = kafkaOptions.GroupId,
                AutoOffsetReset  = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                FetchWaitMaxMs   = 100
            };

            var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            consumer.Subscribe(kafkaOptions.Topic);
            return consumer;
        });

        // --- Strategy: serialisation -----------------------------------------
        // Swap with AvroMessageDeserializer or ProtobufMessageDeserializer here.
        services.AddSingleton<IMessageDeserializer<PlayerScoreData>, JsonMessageDeserializer>();

        // --- Strategy: key mapping -------------------------------------------
        // Swap to support multi-tenant keyspaces or a different naming scheme.
        services.AddSingleton<IRedisKeyMapper, PlatformAccountKeyMapper>();

        // --- Strategy: hash field mapping ------------------------------------
        // Swap to add/remove fields or change the Redis hash schema.
        services.AddSingleton<IRedisHashMapper<PlayerScoreData>, PlayerScoreHashMapper>();

        // --- Repository -------------------------------------------------------
        // Swap to target a different data store (DynamoDB, Cassandra, etc.)
        // by registering a different IScoreRepository implementation here.
        services.AddSingleton<IScoreRepository, RedisScoreRepository>();

        // --- Test data producer (used with --produce flag; no-op in normal consumer mode)
        services.AddSingleton<ITestDataProducer, KafkaTestDataProducer>();

        // --- Consumer service ------------------------------------------------
        services.AddSingleton<IKafkaConsumerService, KafkaConsumerService>();

        return services;
    }
}
