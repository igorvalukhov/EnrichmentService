using EnrichmentService.Abstractions;
using EnrichmentService.Configuration;
using EnrichmentService.Models;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EnrichmentService.Services;

/// <summary>
/// Оркестрирует полный цикл обработки сообщения.
/// </summary>
public sealed class EnrichmentOrchestrator : IEnrichmentOrchestrator
{
    private readonly IExternalApiClient _apiClient;
    private readonly IJsonPathAccessor _pathAccessor;
    private readonly IMessageMerger _merger;
    private readonly EnrichmentSchemaOptions _schema;
    private readonly ObservabilityOptions _observability;
    private readonly ILogger<EnrichmentOrchestrator> _logger;

    private static readonly JsonSerializerOptions LogOpts = new() { WriteIndented = false };

    public EnrichmentOrchestrator(
        IExternalApiClient apiClient,
        IJsonPathAccessor pathAccessor,
        IMessageMerger merger,
        IOptions<EnrichmentSchemaOptions> schema,
        IOptions<ObservabilityOptions> observability,
        ILogger<EnrichmentOrchestrator> logger)
    {
        _apiClient = apiClient;
        _pathAccessor = pathAccessor;
        _merger = merger;
        _schema = schema.Value;
        _observability = observability.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EnrichmentResult> ProcessAsync(
        JsonNode message,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (_observability.LogRawMessages)
            _logger.LogInformation(
                "Raw message: {Body}", Truncate(message));

        JsonNode messageToSend;
        bool wasEnriched = false;
        string? enrichmentError = null;

        try
        {
            var enrichmentData = await FetchAllEnrichmentDataAsync(message, ct);

            messageToSend = enrichmentData.Count > 0
                ? _merger.Merge(message, enrichmentData)
                : message.DeepClone();

            wasEnriched = enrichmentData.Count > 0;

            if (_observability.LogEnrichedMessages)
                _logger.LogInformation(
                    "Enriched message: {Body}", Truncate(messageToSend));
        }
        catch (Exception ex)
        {
            enrichmentError = ex.Message;
            messageToSend = message.DeepClone();

            _logger.LogError(ex,
                "Enrichment failed, falling back to original. Error: {Error}",
                enrichmentError);
        }

        try
        {
            await _apiClient.SendMessageAsync(messageToSend, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to send message to external API.");
            return EnrichmentResult.Failure(messageToSend, ex.Message);
        }

        sw.Stop();

        _logger.LogInformation(
            "Message processed. WasEnriched: {WasEnriched}, Duration: {Duration}ms",
            wasEnriched, sw.ElapsedMilliseconds);

        return enrichmentError is null
            ? wasEnriched
                ? EnrichmentResult.Enriched(messageToSend)
                : EnrichmentResult.FallbackToOriginal(messageToSend, "No enrichment data returned.")
            : EnrichmentResult.FallbackToOriginal(messageToSend, enrichmentError);
    }

    private async Task<IReadOnlyDictionary<string, JsonNode>> FetchAllEnrichmentDataAsync(
        JsonNode message,
        CancellationToken ct)
    {
        if (_schema.Rules.Count == 0)
        {
            _logger.LogWarning("EnrichmentSchema has no rules configured.");
            return new Dictionary<string, JsonNode>();
        }

        var tasks = _schema.Rules.Select(async rule =>
        {
            var sourceNode = _pathAccessor.GetValue(message, rule.SourcePath);
            if (sourceNode is null)
            {
                _logger.LogWarning(
                    "Source field not found. Path: {Path}", rule.SourcePath);
                return (rule.DestinationPath, Data: (JsonNode?)null);
            }

            var data = await _apiClient.FetchEnrichmentDataAsync(
                sourceNode.ToString(), ct);

            return (rule.DestinationPath, Data: data);
        });

        var results = await Task.WhenAll(tasks);

        return results
            .Where(r => r.Data is not null)
            .ToDictionary(r => r.DestinationPath, r => r.Data!);
    }

    private string Truncate(JsonNode node)
    {
        var json = node.ToJsonString(LogOpts);
        return json.Length <= _observability.MaxLoggedMessageLength
            ? json
            : string.Concat(json.AsSpan(0, _observability.MaxLoggedMessageLength), "...[truncated]");
    }
}