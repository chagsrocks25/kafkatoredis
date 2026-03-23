using KafkaToRedis.Configuration;
using KafkaToRedis.Infrastructure;
using KafkaToRedis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

// ---------------------------------------------------------------------------
// Mode detection  (default = consume)
// Usage:
//   dotnet run              → consume from Kafka and write to Redis
//   dotnet run -- --produce → produce test scenario messages to Kafka
// ---------------------------------------------------------------------------
var isProduceMode = args.Contains("--produce");

// ---------------------------------------------------------------------------
// Configuration  (appsettings.json → env vars override)
// ---------------------------------------------------------------------------
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());

// ---------------------------------------------------------------------------
// Redis — async connection must be done before the DI container is built
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
    "KafkaToRedis starting — mode={Mode} topic={Topic} brokers={Brokers} redis={Redis}",
    isProduceMode ? "PRODUCE" : "CONSUME",
    kafkaOptions.Topic,
    kafkaOptions.BootstrapServers,
    redisOptions.ConnectionString);

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
if (isProduceMode)
{
    // Produce the test scenario then exit — no long-running loop needed.
    var testProducer = provider.GetRequiredService<ITestDataProducer>();
    await testProducer.ProduceAsync(cts.Token);
}
else
{
    // Consume continuously until Ctrl+C.
    var consumerService = provider.GetRequiredService<IKafkaConsumerService>();
    await consumerService.ConsumeAsync(cts.Token);
}

logger.LogInformation("KafkaToRedis stopped cleanly.");
