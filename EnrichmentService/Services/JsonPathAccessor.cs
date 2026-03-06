using EnrichmentService.Abstractions;
using System.Text.Json.Nodes;

namespace EnrichmentService.Services;

public sealed class JsonPathAccessor : IJsonPathAccessor
{
    /// <inheritdoc />
    public JsonNode? GetValue(JsonNode root, string path)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        JsonNode? current = root;

        foreach (var segment in segments)
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(segment, out var next))
                current = next;
            else
                return null;
        }

        return current;
    }

    /// <inheritdoc />
    public void SetValue(JsonNode root, string path, JsonNode value)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);

        if (root is not JsonObject rootObj)
            throw new InvalidOperationException(
                $"Root node must be JsonObject. Actual: {root.GetType().Name}");

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = rootObj;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (!current.TryGetPropertyValue(segment, out var next) || next is not JsonObject nextObj)
            {
                nextObj = new JsonObject();
                current[segment] = nextObj;
            }
            current = nextObj;
        }

        current[segments[^1]] = value.DeepClone();
    }
}