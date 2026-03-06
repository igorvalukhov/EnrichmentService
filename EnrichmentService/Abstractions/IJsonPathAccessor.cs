using System.Text.Json.Nodes;

namespace EnrichmentService.Abstractions
{
    public interface IJsonPathAccessor
    {
        JsonNode? GetValue(JsonNode root, string path);
        void SetValue(JsonNode root, string path, JsonNode value);
    }
}
