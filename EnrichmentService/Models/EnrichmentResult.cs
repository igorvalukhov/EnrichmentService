using System.Text.Json.Nodes;

namespace EnrichmentService.Models;

/// <summary>
/// Результат обработки сообщения.
/// </summary>
public sealed class EnrichmentResult
{
    public bool IsSuccess { get; init; }
    public required JsonNode Payload { get; init; }
    public bool WasEnriched { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Сообщение успешно обогащено и отправлено.
    /// </summary>
    public static EnrichmentResult Enriched(JsonNode payload) =>
        new() { IsSuccess = true, WasEnriched = true, Payload = payload };

    /// <summary>
    /// JSON успешно распарсен, готов к обработке.
    /// </summary>
    public static EnrichmentResult Parsed(JsonNode payload) =>
        new() { IsSuccess = true, WasEnriched = false, Payload = payload };

    /// <summary>
    /// Обогащение не удалось, отправлен оригинал.
    /// </summary>
    public static EnrichmentResult FallbackToOriginal(JsonNode original, string error) =>
        new() { IsSuccess = true, WasEnriched = false, Payload = original, ErrorMessage = error };

    /// <summary>
    /// Отправка во внешний API не удалась.
    /// </summary>
    public static EnrichmentResult Failure(JsonNode payload, string error) =>
        new() { IsSuccess = false, WasEnriched = false, Payload = payload, ErrorMessage = error };
}