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

public sealed class ExternalApiOptions
{
    public const string SectionName = "ExternalApi";

    public required string BaseUrl { get; init; }
    public string FetchEndpointTemplate { get; init; } = "/api/enrich/{value}";
    public string SendEndpoint { get; init; } = "/api/messages/enriched";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public int RetryCount { get; init; } = 3;
}
