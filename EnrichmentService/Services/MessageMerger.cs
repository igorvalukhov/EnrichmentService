using EnrichmentService.Abstractions;
using System.Text.Json.Nodes;

namespace EnrichmentService.Services;

public sealed class MessageMerger : IMessageMerger
{
    private readonly IJsonPathAccessor _accessor;

    public MessageMerger(IJsonPathAccessor accessor) => _accessor = accessor;

    public JsonNode Merge(JsonNode original, IReadOnlyDictionary<string, JsonNode> enrichmentData)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(enrichmentData);

        if (original is not JsonObject)
            throw new InvalidOperationException(
                $"Root node must be JsonObject. Actual: {original.GetType().Name}");

        var merged = original.DeepClone();

        foreach (var (destinationPath, data) in enrichmentData)
            _accessor.SetValue(merged, destinationPath, data);

        return merged;
    }
}