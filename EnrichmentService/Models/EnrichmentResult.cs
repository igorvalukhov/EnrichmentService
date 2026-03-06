using System.Text.Json.Nodes;

namespace EnrichmentService.Models;

public sealed class EnrichmentResult
{
    public bool IsSuccess { get; init; }
    public required JsonNode Payload { get; init; }
    public bool WasEnriched { get; init; }
    public string? ErrorMessage { get; init; }

    public static EnrichmentResult Enriched(JsonNode payload) =>
        new() { IsSuccess = true, WasEnriched = true, Payload = payload };

    public static EnrichmentResult FallbackToOriginal(JsonNode original, string error) =>
        new() { IsSuccess = true, WasEnriched = false, Payload = original, ErrorMessage = error };

    public static EnrichmentResult Failure(JsonNode payload, string error) =>
        new() { IsSuccess = false, WasEnriched = false, Payload = payload, ErrorMessage = error };
}