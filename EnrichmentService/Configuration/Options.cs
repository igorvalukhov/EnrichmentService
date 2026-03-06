namespace EnrichmentService.Configuration;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public required string BootstrapServers { get; init; }
    public required string InputTopic { get; init; }
    public required string ConsumerGroup { get; init; }
    public int PollTimeoutMs { get; init; } = 1000;
}