using System.Text.Json.Nodes;

namespace EnrichmentService.Abstractions
{
    /// <summary>
    /// Сливает оригинальное сообщение с данными обогащения.
    /// </summary>
    public interface IMessageMerger
    {
        /// <summary>
        /// Создаёт копию оригинала и записывает данные обогащения по указанным путям.
        /// </summary>
        JsonNode Merge(JsonNode original, IReadOnlyDictionary<string, JsonNode> enrichmentData);
    }
}
