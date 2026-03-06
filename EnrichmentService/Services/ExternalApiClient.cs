using EnrichmentService.Abstractions;
using EnrichmentService.Configuration;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EnrichmentService.Services;

public sealed class ExternalApiClient : IExternalApiClient
{
    private readonly HttpClient _http;
    private readonly ExternalApiOptions _options;
    private readonly ILogger<ExternalApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public ExternalApiClient(
        HttpClient http,
        IOptions<ExternalApiOptions> options,
        ILogger<ExternalApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JsonNode?> FetchEnrichmentDataAsync(
        string sourceValue,
        CancellationToken ct = default)
    {
        var endpoint = _options.FetchEndpointTemplate
            .Replace("{value}", Uri.EscapeDataString(sourceValue));

        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Fetching enrichment data. Value: {Value}, Endpoint: {Endpoint}",
            sourceValue, endpoint);

        try
        {
            var response = await _http.GetAsync(endpoint, ct);
            sw.Stop();

            _logger.LogInformation(
                "Enrichment data fetch completed. StatusCode: {StatusCode}, Duration: {Duration}ms",
                (int)response.StatusCode, sw.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "External API returned non-success status. " +
                    "Value: {Value}, StatusCode: {StatusCode}",
                    sourceValue, (int)response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            return JsonNode.Parse(content);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Failed to fetch enrichment data. Value: {Value}, Duration: {Duration}ms",
                sourceValue, sw.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(JsonNode message, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Sending message to external API. Endpoint: {Endpoint}",
            _options.SendEndpoint);

        try
        {
            var response = await _http.PostAsJsonAsync(
                _options.SendEndpoint, message, JsonOpts, ct);

            sw.Stop();

            _logger.LogInformation(
                "Send completed. StatusCode: {StatusCode}, Duration: {Duration}ms",
                (int)response.StatusCode, sw.ElapsedMilliseconds);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Failed to send message to external API. Duration: {Duration}ms",
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}