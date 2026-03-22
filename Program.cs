using KafkaToRedis.Configuration;
using KafkaToRedis.Infrastructure;
using KafkaToRedis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());

// ---------------------------------------------------------------------------
// Redis — requires async initialisation before the DI container is built
// ---------------------------------------------------------------------------
var redisOptions = configuration.GetSection("Redis").Get<RedisOptions>()
                   ?? new RedisOptions();
try
{
    var redis = await ConnectionMultiplexer.ConnectAsync(redisOptions.ConnectionString);
    services.AddSingleton<IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[FATAL] Redis connection failed: {ex.Message}");
    Environment.Exit(1);
}

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
services.AddKafkaToRedis(configuration);

var provider = services.BuildServiceProvider();
var logger   = provider.GetRequiredService<ILogger<Program>>();

var kafkaOptions = configuration.GetSection("Kafka").Get<KafkaOptions>() ?? new KafkaOptions();
logger.LogInformation(
    "KafkaToRedis started. Topic={Topic} Brokers={Brokers} Redis={Redis}",
    kafkaOptions.Topic, kafkaOptions.BootstrapServers, redisOptions.ConnectionString);

// ---------------------------------------------------------------------------
// Graceful shutdown on Ctrl+C / SIGTERM
// ---------------------------------------------------------------------------
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // prevent immediate process kill
    cts.Cancel();
};

// ---------------------------------------------------------------------------
// Run
// ---------------------------------------------------------------------------
var consumerService = provider.GetRequiredService<IKafkaConsumerService>();
await consumerService.ConsumeAsync(cts.Token);

logger.LogInformation("KafkaToRedis stopped cleanly.");
