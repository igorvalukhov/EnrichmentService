using System.Text.Json.Nodes;

namespace EnrichmentService.Models;

public sealed class EnrichmentResult
{
    public bool IsSuccess { get; init; }
    public required JsonNode Payload { get; init; }
    public string? ErrorMessage { get; init; }

    public static EnrichmentResult Success(JsonNode payload) =>
        new() { IsSuccess = true, Payload = payload };

    public static EnrichmentResult Failure(string error, JsonNode payload) =>
        new() { IsSuccess = false, Payload = payload, ErrorMessage = error };
}