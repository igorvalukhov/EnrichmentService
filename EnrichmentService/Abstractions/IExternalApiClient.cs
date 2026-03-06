using System.Text.Json.Nodes;

namespace EnrichmentService.Abstractions;

public interface IExternalApiClient
{
    Task<JsonNode?> FetchEnrichmentDataAsync(
        string sourceValue,
        CancellationToken ct = default);

    Task SendMessageAsync(
        JsonNode message,
        CancellationToken ct = default);
}