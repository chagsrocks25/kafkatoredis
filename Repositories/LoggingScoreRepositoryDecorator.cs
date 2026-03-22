using KafkaToRedis.Domain;
using Microsoft.Extensions.Logging;

namespace KafkaToRedis.Repositories;

/// <summary>
/// Decorator that wraps any <see cref="IScoreRepository"/> to add structured
/// logging and timing without modifying the underlying implementation.
///
/// Register by wrapping the concrete repository in the DI composition root:
/// <code>
///   services.AddSingleton&lt;IScoreRepository&gt;(sp =>
///       new LoggingScoreRepositoryDecorator(
///           sp.GetRequiredService&lt;RedisScoreRepository&gt;(), ...));
/// </code>
///
/// Additional decorators (metrics, circuit breaker, retry) can be stacked the
/// same way following the Decorator pattern.
/// </summary>
public sealed class LoggingScoreRepositoryDecorator : IScoreRepository
{
    private readonly IScoreRepository                       _inner;
    private readonly ILogger<LoggingScoreRepositoryDecorator> _logger;

    public LoggingScoreRepositoryDecorator(
        IScoreRepository                          inner,
        ILogger<LoggingScoreRepositoryDecorator> logger)
    {
        _inner  = inner;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task WriteAsync(
        string          redisKey,
        PlayerScoreData data,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Writing score '{ScoreId}' → {Key}", data.ScoreId, redisKey);

        await _inner.WriteAsync(redisKey, data, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string redisKey,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Tombstone → deleting {Key}", redisKey);
        await _inner.DeleteAsync(redisKey, cancellationToken);
    }
}
