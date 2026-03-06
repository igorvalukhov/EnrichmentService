namespace EnrichmentService.Configuration;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public required string BootstrapServers { get; init; }
    public required string InputTopic { get; init; }
    public required string ConsumerGroup { get; init; }
    public int PollTimeoutMs { get; init; } = 1000;
}

public sealed class EnrichmentSchemaOptions
{
    public const string SectionName = "EnrichmentSchema";

    public List<EnrichmentRuleOptions> Rules { get; init; } = [];
}

public sealed class EnrichmentRuleOptions
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
}