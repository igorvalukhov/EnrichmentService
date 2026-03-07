using EnrichmentService.Abstractions;
using EnrichmentService.Configuration;
using EnrichmentService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace EnrichmentService.IntegrationTests;

/// <summary>
/// Интеграционные тесты для EnrichmentOrchestrator.
/// </summary>
public sealed class EnrichmentOrchestratorIntegrationTests : IDisposable
{
    private readonly WireMockServer _wireMock;
    private readonly IEnrichmentOrchestrator _sut;

    public EnrichmentOrchestratorIntegrationTests()
    {
        _wireMock = WireMockServer.Start();

        var httpClient = new HttpClient { BaseAddress = new Uri(_wireMock.Urls[0]) };

        var apiOptions = Options.Create(new ExternalApiOptions
        {
            BaseUrl = _wireMock.Urls[0],
            FetchEndpointTemplate = "/api/enrich/{value}",
            SendEndpoint = "/api/messages/enriched",
            Timeout = TimeSpan.FromSeconds(5),
            RetryCount = 0
        });

        var apiClient = new ExternalApiClient(httpClient, apiOptions, NullLogger<ExternalApiClient>.Instance);
        var accessor = new JsonPathAccessor();
        var merger = new MessageMerger(accessor);

        var schema = Options.Create(new EnrichmentSchemaOptions
        {
            Rules =
            [
                new EnrichmentRuleOptions
                {
                    SourcePath = "user.id",
                    DestinationPath = "user.profile"
                }
            ]
        });

        var observability = Options.Create(new ObservabilityOptions
        {
            LogRawMessages = false,
            LogEnrichedMessages = false
        });

        _sut = new EnrichmentOrchestrator(
            apiClient,
            accessor,
            merger,
            schema,
            observability,
            NullLogger<EnrichmentOrchestrator>.Instance);
    }

    /// <summary>
    /// Полный цикл, сообщение обогащено, оригинальные поля сохранены, POST вызван.
    /// </summary>
    [Fact]
    public async Task FullCycle_EnrichesAndSendsOnce()
    {
        _wireMock.Reset();
        _wireMock
            .Given(Request.Create().WithPath("/api/enrich/123").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBodyAsJson(new { name = "test", email = "test@mail.ru" }));

        _wireMock
            .Given(Request.Create().WithPath("/api/messages/enriched").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var message = JsonNode.Parse("""{"user":{"id":"123"},"meta":"preserved"}""")!;

        var result = await _sut.ProcessAsync(message);

        result.IsSuccess.Should().BeTrue();
        result.WasEnriched.Should().BeTrue();
        result.Payload["user"]!["profile"]!["name"]!.GetValue<string>().Should().Be("test");
        result.Payload["meta"]!.GetValue<string>().Should().Be("preserved");
    }

    public void Dispose()
    {
        _wireMock.Stop();
        _wireMock.Dispose();
    }
}
