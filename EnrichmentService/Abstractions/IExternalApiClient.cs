using System.Text.Json.Nodes;

namespace EnrichmentService.Abstractions;

/// <summary>
/// HTTP клиент для взаимодействия с внешним API обогащения.
/// </summary>
public interface IExternalApiClient
{
    /// <summary>
    /// Получает данные обогащения из внешнего API.
    /// </summary>
    Task<JsonNode?> FetchEnrichmentDataAsync(
        string sourceValue,
        CancellationToken ct = default);

    /// <summary>
    /// Отправляет сообщение во внешний API.
    /// </summary>
    Task SendMessageAsync(
        JsonNode message,
        CancellationToken ct = default);
}