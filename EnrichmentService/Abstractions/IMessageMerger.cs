using System.Text.Json.Nodes;

namespace EnrichmentService.Abstractions
{
    public interface IMessageMerger
    {
        JsonNode Merge(JsonNode original, IReadOnlyDictionary<string, JsonNode> enrichmentData);
    }
}
