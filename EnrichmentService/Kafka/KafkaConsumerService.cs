using Confluent.Kafka;
using EnrichmentService.Configuration;
using EnrichmentService.Models;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace EnrichmentService.Kafka;

public sealed class KafkaConsumerService : BackgroundService
{
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<KafkaConsumerService> _logger;

    public KafkaConsumerService(
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<KafkaConsumerService> logger)
    {
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Kafka consumer starting. Topic: {Topic}, Group: {Group}, Brokers: {Brokers}",
            _kafkaOptions.InputTopic,
            _kafkaOptions.ConsumerGroup,
            _kafkaOptions.BootstrapServers);

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = _kafkaOptions.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
                _logger.LogError(
                    "Kafka error. Code: {Code}, Reason: {Reason}",
                    e.Code, e.Reason))
            .Build();

        consumer.Subscribe(_kafkaOptions.InputTopic);

        try
        {
            await ConsumeLoopAsync(consumer, stoppingToken);
        }
        finally
        {
            consumer.Close();
            _logger.LogInformation("Kafka consumer stopped.");
        }
    }

    private async Task ConsumeLoopAsync(
        IConsumer<string, string> consumer,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult = null;

            try
            {
                consumeResult = consumer.Consume(
                    TimeSpan.FromMilliseconds(_kafkaOptions.PollTimeoutMs));
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex,
                    "Kafka consume error. Offset: {Offset}",
                    ex.ConsumerRecord?.Offset);
                continue;
            }

            if (consumeResult is null) continue;

            _logger.LogInformation(
                "Raw message received. Topic: {Topic}, Partition: {Partition}, " +
                "Offset: {Offset}, Key: {Key}",
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value,
                consumeResult.Message.Key);

            var parseResult = TryParseJson(consumeResult.Message.Value, consumeResult.Offset.Value);

            if (!parseResult.IsSuccess)
            {
                consumer.Commit(consumeResult);
                continue;
            }

            _logger.LogInformation(
                "Message parsed successfully. Offset: {Offset}, Body: {Body}",
                consumeResult.Offset.Value,
                parseResult.Payload.ToJsonString());

            consumer.Commit(consumeResult);

            _logger.LogDebug(
                "Offset committed. Partition: {Partition}, Offset: {Offset}",
                consumeResult.Partition.Value,
                consumeResult.Offset.Value);

            await Task.Delay(10, stoppingToken);
        }
    }

    private EnrichmentResult TryParseJson(string rawValue, long offset)
    {
        try
        {
            var node = JsonNode.Parse(rawValue);

            if (node is null)
            {
                _logger.LogWarning(
                    "Parsed JSON is null. Offset: {Offset}. Skipping.",
                    offset);
                return EnrichmentResult.Failure(JsonValue.Create("")!, "Parsed JSON is null.");
            }

            if (node is not JsonObject)
            {
                _logger.LogWarning(
                    "Message is not a JSON object. Offset: {Offset}. Skipping.",
                    offset);
                return EnrichmentResult.Failure(node, "Root must be a JSON object.");
            }

            return EnrichmentResult.Enriched(node);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to parse message as JSON. Offset: {Offset}. Skipping.",
                offset);
            return EnrichmentResult.Failure(JsonValue.Create(rawValue)!, ex.Message);
        }
    }
}