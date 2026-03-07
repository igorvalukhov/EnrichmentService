using EnrichmentService.Models;
using System.Text.Json.Nodes;

namespace EnrichmentService.Abstractions
{
    /// <summary>
    /// Оркестрирует полный цикл обработки сообщения.
    /// </summary>
    public interface IEnrichmentOrchestrator
    {
        /// <summary>
        /// Обогащает сообщение и отправляет результат во внешний API.
        /// </summary>
        Task<EnrichmentResult> ProcessAsync(JsonNode message, CancellationToken ct = default);
    }
}
