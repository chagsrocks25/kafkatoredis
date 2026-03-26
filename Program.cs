using KafkaToRedis.Configuration;
using KafkaToRedis.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

/// <summary>
/// Application entry point. Builds the DI container and runs the Kafka consumer.
/// Redis is connected before <see cref="ServiceProvider"/> is built because
/// <see cref="ConnectionMultiplexer.ConnectAsync"/> is async and
/// <see cref="IServiceCollection"/> has no async registration mechanism.
/// </summary>
public sealed class Program
{
    public static async Task Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        // Redis — must be connected before BuildServiceProvider.
        var redisOptions = configuration.GetSection("Redis").Get<RedisOptions>() ?? new RedisOptions();
        try
        {
            var configOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            if (redisOptions.ConnectTimeoutMs.HasValue)
            {
                configOptions.ConnectTimeout = redisOptions.ConnectTimeoutMs.Value;
            }
            
            var redis = await ConnectionMultiplexer.ConnectAsync(configOptions);
            services.AddSingleton<IConnectionMultiplexer>(redis);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[FATAL] Redis connection failed: {ex.Message}");
            Environment.Exit(1);
        }

        services.AddKafkaToRedis(configuration);

        var provider = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // hand control to the consumer's shutdown path
            cts.Cancel();
        };

        try
        {
            await provider.GetRequiredService<IKafkaConsumerService>().ConsumeAsync(cts.Token);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[FATAL] Unhandled exception: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
