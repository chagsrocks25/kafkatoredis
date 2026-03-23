using KafkaToRedis.Domain;
using KafkaToRedis.Mapping;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace KafkaToRedis.Repositories;

/// <summary>
/// Redis implementation of <see cref="IScoreRepository"/>.
///
/// Redis key:    <c>sbmm:{accountId}:{platformId}</c>  (no TTL applied)
/// Hash fields:  <c>{scoreId}:normalized_value</c>, <c>{scoreId}:raw_value</c>, …
///
/// The hash-field layout is delegated to <see cref="IRedisHashMapper{TValue}"/>,
/// making it independently replaceable without touching this class.
/// </summary>
public sealed class RedisScoreRepository : IScoreRepository
{
    private readonly IDatabase                         _db;
    private readonly IRedisHashMapper<PlayerScoreData> _hashMapper;
    private readonly ILogger<RedisScoreRepository>    _logger;

    public RedisScoreRepository(
        IConnectionMultiplexer            multiplexer,
        IRedisHashMapper<PlayerScoreData> hashMapper,
        ILogger<RedisScoreRepository>    logger)
    {
        _db         = multiplexer.GetDatabase();
        _hashMapper = hashMapper;
        _logger     = logger;
    }

    /// <inheritdoc/>
    public async Task WriteAsync(
        string          redisKey,
        PlayerScoreData data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = _hashMapper.Map(data);
        await _db.HashSetAsync(redisKey, entries);
        _logger.LogDebug(
            "Upserted key {Key} score prefix '{ScoreId}'.", redisKey, data.ScoreId);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string redisKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _db.KeyDeleteAsync(redisKey);
        _logger.LogDebug("Deleted key {Key}.", redisKey);
    }
}
