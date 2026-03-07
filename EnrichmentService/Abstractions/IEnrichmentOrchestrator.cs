using EnrichmentService.Models;
using System.Text.Json.Nodes;

namespace EnrichmentService.Abstractions
{
    public interface IEnrichmentOrchestrator
    {
        Task<EnrichmentResult> ProcessAsync(JsonNode message, CancellationToken ct = default);
    }
}
