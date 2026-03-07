using System.Text.Json.Nodes;

namespace EnrichmentService.Abstractions
{
    /// <summary>
    /// Доступ к полям JSON по dot notation путям.
    /// </summary>
    public interface IJsonPathAccessor
    {
        /// <summary>
        /// Возвращает значение по dot notation пути.
        /// </summary>
        JsonNode? GetValue(JsonNode root, string path);

        /// <summary>
        /// Записывает значение по dot notation пути.
        /// </summary>
        void SetValue(JsonNode root, string path, JsonNode value);
    }
}
