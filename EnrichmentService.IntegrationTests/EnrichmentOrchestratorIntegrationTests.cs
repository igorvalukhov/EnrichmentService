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
                },
                new EnrichmentRuleOptions
                {
                    SourcePath = "cityId",
                    DestinationPath = "cityDetails"
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

    /// <summary>
    /// GET обогащения вернул ошибку и отправляется оригинальное сообщение без обогащения.
    /// </summary>
    [Fact]
    public async Task WhenFetchFails_SendsOriginalMessage()
    {
        _wireMock.Reset();
        _wireMock
            .Given(Request.Create().WithPath("/api/enrich/567").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        JsonNode? capturedBody = null;
        _wireMock
            .Given(Request.Create().WithPath("/api/messages/enriched").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithCallback(req =>
                {
                    capturedBody = JsonNode.Parse(req.Body ?? "{}");
                    return new WireMock.ResponseMessage { StatusCode = 200 };
                }));

        var message = JsonNode.Parse("""{"user":{"id":"567"},"important":"data"}""")!;

        var result = await _sut.ProcessAsync(message);
        result.IsSuccess.Should().BeTrue();
        result.WasEnriched.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// При нескольких правилах обогащения POST во внешний API всё равно вызывается ровно один раз.
    /// </summary>
    [Fact]
    public async Task FullCycle_PostSentExactlyOnce_EvenWithMultipleRules()
    {
        _wireMock.Reset();

        _wireMock
            .Given(Request.Create().WithPath("/api/enrich/123").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBodyAsJson(new { name = "test" }));

        _wireMock
            .Given(Request.Create().WithPath("/api/enrich/777").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBodyAsJson(new { cityName = "Moscow" }));

        _wireMock
            .Given(Request.Create().WithPath("/api/messages/enriched").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var message = JsonNode.Parse("""{"user":{"id":"123"},"cityId":"777"}""")!;

        await _sut.ProcessAsync(message);

        var getCount = _wireMock.LogEntries
            .Count(e => e.RequestMessage.Method == "GET");

        var postCount = _wireMock.LogEntries
            .Count(e => e.RequestMessage.Path == "/api/messages/enriched"
                        && e.RequestMessage.Method == "POST");

        getCount.Should().Be(2);
        postCount.Should().Be(1);
    }

    /// <summary>
    /// GET обогащения вернул ошибку, POST всё равно вызывается однократно с оригиналом.
    /// </summary>
    [Fact]
    public async Task WhenFetchFails_PostStillCalledOnce()
    {
        _wireMock.Reset();
        _wireMock
            .Given(Request.Create().WithPath("/api/enrich/789").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        _wireMock
            .Given(Request.Create().WithPath("/api/messages/enriched").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var message = JsonNode.Parse("""{"user":{"id":"789"}}""")!;

        await _sut.ProcessAsync(message);

        var postCount = _wireMock.LogEntries
            .Count(e => e.RequestMessage.Path == "/api/messages/enriched"
                        && e.RequestMessage.Method == "POST");

        postCount.Should().Be(1);
    }

    public void Dispose()
    {
        _wireMock.Stop();
        _wireMock.Dispose();
    }
}
