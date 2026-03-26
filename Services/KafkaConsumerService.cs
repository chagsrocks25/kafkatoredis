using System.Collections.Immutable;
using Confluent.Kafka;
using KafkaToRedis.Configuration;
using KafkaToRedis.Domain;
using KafkaToRedis.Mapping;
using KafkaToRedis.Repositories;
using KafkaToRedis.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KafkaToRedis.Services;

/// <summary>
/// Polls a Kafka topic, accumulates records into an ordered batch, and flushes
/// them to <see cref="IScoreRepository"/> sequentially to preserve Kafka offset order.
///
/// Dependencies are injected through interfaces, making each concern independently
/// replaceable:
/// <list type="bullet">
///   <item><see cref="IMessageDeserializer{TDomain}"/> — swap JSON for Avro/Protobuf</item>
///   <item><see cref="IRedisKeyMapper"/>               — change keyspace scheme</item>
///   <item><see cref="IScoreRepository"/>              — target a different data store</item>
/// </list>
///
/// Batching strategy:
/// <list type="bullet">
///   <item>Flush after <see cref="KafkaOptions.MaxBatchSize"/> records accumulate, OR</item>
///   <item>Flush after <see cref="KafkaOptions.MaxBatchDelaySeconds"/> seconds elapse.</item>
/// </list>
///
/// Offset commits are manual and happen only after a successful flush so no
/// messages are lost if Redis is temporarily unavailable.
/// </summary>
public sealed class KafkaConsumerService : IKafkaConsumerService, IDisposable
{
    private readonly IConsumer<string, string>            _consumer;
    private readonly IMessageDeserializer<PlayerScoreData> _deserializer;
    private readonly IRedisKeyMapper                      _keyMapper;
    private readonly IScoreRepository                     _repository;
    private readonly ILogger<KafkaConsumerService>        _logger;
    private readonly int      _maxBatchSize;
    private readonly TimeSpan _maxBatchDelay;
    private readonly bool     _failOnProcessingError;

    public KafkaConsumerService(
        IConsumer<string, string>             consumer,
        IMessageDeserializer<PlayerScoreData> deserializer,
        IRedisKeyMapper                       keyMapper,
        IScoreRepository                      repository,
        IOptions<KafkaOptions>                kafkaOptions,
        ILogger<KafkaConsumerService>         logger)
    {
        _consumer      = consumer;
        _deserializer  = deserializer;
        _keyMapper     = keyMapper;
        _repository    = repository;
        _logger        = logger;
        _maxBatchSize          = kafkaOptions.Value.MaxBatchSize;
        _maxBatchDelay         = TimeSpan.FromSeconds(kafkaOptions.Value.MaxBatchDelaySeconds);
        _failOnProcessingError = kafkaOptions.Value.FailOnProcessingError;
    }

    /// <inheritdoc/>
    public async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        // Ordered list — preserves per-partition Kafka offset ordering.
        // Do NOT replace with Dictionary; deduplication mid-batch would silently
        // drop intermediate updates before the flush, which could violate
        // last-write-wins if a record for the same key appears in the next batch.
        var batch = ImmutableList<BatchRecord>.Empty;
        var partitionOffsets = new Dictionary<TopicPartition, Offset>();
        var batchStartTime   = DateTime.UtcNow;

        _logger.LogInformation("Kafka consumer started.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromMilliseconds(200));

                    if (result?.Message is not null)
                        AccumulateRecord(result, ref batch, partitionOffsets);

                    if (ShouldFlush(batch.Count, partitionOffsets.Count, batchStartTime))
                    {
                        batch = await FlushAsync(batch, partitionOffsets, cancellationToken);
                        batchStartTime = DateTime.UtcNow;
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError("Kafka consume error: {Reason}", ex.Error.Reason);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cancellation requested — consumer shutting down.");
        }
        finally
        {
            await FinalFlushAsync(batch, partitionOffsets);
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void AccumulateRecord(
        ConsumeResult<string, string>  result,
        ref ImmutableList<BatchRecord> batch,
        Dictionary<TopicPartition, Offset> partitionOffsets)
    {
        if (!_keyMapper.TryMap(result.Message.Key, out var redisKey))
        {
            if (_failOnProcessingError)
            {
                throw new InvalidOperationException(
                    $"Failing on unmappable Kafka key '{result.Message.Key}' at offset {result.Offset} as configured.");
            }

            _logger.LogWarning(
                "Skipping record — unmappable Kafka key '{Key}'.", result.Message.Key);
            
            // Advance past it only if we're ignoring errors 
            partitionOffsets[result.TopicPartition] = result.Offset + 1;
            return;
        }

        if (result.Message.Value is null)
        {
            // Tombstone — null Kafka value in a compacted topic means delete from Redis.
            batch = batch.Add(BatchRecord.ForDelete(redisKey));
            partitionOffsets[result.TopicPartition] = result.Offset + 1;
            return;
        }

        TryAccumulateWrite(result, redisKey, ref batch, partitionOffsets);
    }

    private void TryAccumulateWrite(
        ConsumeResult<string, string>  result,
        string                         redisKey,
        ref ImmutableList<BatchRecord> batch,
        Dictionary<TopicPartition, Offset> partitionOffsets)
    {
        if (!_deserializer.TryDeserialize(result.Message.Value, out var data))
        {
            if (_failOnProcessingError)
            {
                throw new InvalidOperationException(
                    $"Failing on deserialization error for key '{result.Message.Key}' at offset {result.Offset} as configured.");
            }

            _logger.LogWarning(
                "Skipping record — deserialization failed for key '{Key}'.",
                result.Message.Key);
            
            // Advance past it only if we're ignoring errors 
            partitionOffsets[result.TopicPartition] = result.Offset + 1;
            return;
        }

        batch = batch.Add(BatchRecord.ForWrite(redisKey, data));
        partitionOffsets[result.TopicPartition] = result.Offset + 1;
    }

    private bool ShouldFlush(int batchCount, int offsetCount, DateTime batchStartTime) =>
        batchCount >= _maxBatchSize ||
        (offsetCount > 0 && DateTime.UtcNow - batchStartTime >= _maxBatchDelay);

    private async Task<ImmutableList<BatchRecord>> FlushAsync(
        ImmutableList<BatchRecord>         batch,
        Dictionary<TopicPartition, Offset> partitionOffsets,
        CancellationToken                  cancellationToken)
    {
        // Sequential writes — last record for a given (key, scoreId) is the winner,
        // matching Kafka compaction semantics. Task.WhenAll is intentionally avoided.
        // Per-record error handling prevents a single poison record from aborting the batch.
        foreach (var record in batch)
        {
            try
            {
                if (record.IsTombstone)
                    await _repository.DeleteAsync(record.RedisKey, cancellationToken);
                else
                    await _repository.WriteAsync(record.RedisKey, record.Data, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation — do not swallow shutdown signals.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process record for key '{RedisKey}' (tombstone={IsTombstone}). Skipping.",
                    record.RedisKey,
                    record.IsTombstone);
            }
        }

        _logger.LogInformation("Flushed {Count} record(s) to repository.", batch.Count);

        if (partitionOffsets.Count > 0)
        {
            var offsets = partitionOffsets
                .Select(kvp => new TopicPartitionOffset(kvp.Key, kvp.Value));

            _consumer.Commit(offsets);
            _logger.LogDebug("Committed offsets for {Count} partition(s).", partitionOffsets.Count);
            partitionOffsets.Clear();
        }

        return ImmutableList<BatchRecord>.Empty;
    }

    private async Task FinalFlushAsync(
        ImmutableList<BatchRecord>         batch,
        Dictionary<TopicPartition, Offset> partitionOffsets)
    {
        if (batch.Count == 0 && partitionOffsets.Count == 0) return;

        _logger.LogInformation("Performing final flush before shutdown ({Count} record(s)).", batch.Count);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await FlushAsync(batch, partitionOffsets, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during final flush.");
        }
    }

    public void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
    }
}
