using EnrichmentService.Abstractions;
using EnrichmentService.Configuration;
using EnrichmentService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json.Nodes;

namespace EnrichmentService.UnitTests;

public sealed class EnrichmentOrchestratorTests
{
    private readonly Mock<IExternalApiClient> _apiClientMock = new(MockBehavior.Strict);

    private EnrichmentOrchestrator CreateSut() => new(
        _apiClientMock.Object,
        new JsonPathAccessor(),
        new MessageMerger(new JsonPathAccessor()),
        Options.Create(new EnrichmentSchemaOptions
        {
            Rules =
            [
                new EnrichmentRuleOptions
                {
                    SourcePath = "id",
                    DestinationPath = "userDetails"
                }
            ]
        }),
        NullLogger<EnrichmentOrchestrator>.Instance);

    [Fact]
    public async Task ProcessAsync_HappyPath_ReturnsEnrichedMessage()
    {
        var message = JsonNode.Parse("""{"id":"123","Name":"test"}""")!;
        var enrichData = JsonNode.Parse("""{"age":25,"city":"Moscow"}""")!;

        _apiClientMock
            .Setup(c => c.FetchEnrichmentDataAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(enrichData);
        _apiClientMock
            .Setup(c => c.SendMessageAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateSut().ProcessAsync(message);

        result.IsSuccess.Should().BeTrue();
        result.WasEnriched.Should().BeTrue();
        result.Payload["userDetails"]!["age"]!.GetValue<int>().Should().Be(25);
        result.Payload["Name"]!.GetValue<string>().Should().Be("test");
    }

    [Fact]
    public async Task ProcessAsync_SendCalledExactlyOnce()
    {
        var message = JsonNode.Parse("""{"id":"123"}""")!;

        _apiClientMock
            .Setup(c => c.FetchEnrichmentDataAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonNode.Parse("""{"age":25}""")!);
        _apiClientMock
            .Setup(c => c.SendMessageAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateSut().ProcessAsync(message);

        _apiClientMock.Verify(
            c => c.SendMessageAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_FetchFails_SendsOriginalMessage()
    {
        var message = JsonNode.Parse("""{"id":"123","Name":"test"}""")!;

        _apiClientMock
            .Setup(c => c.FetchEnrichmentDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        JsonNode? sentMessage = null;
        _apiClientMock
            .Setup(c => c.SendMessageAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback<JsonNode, CancellationToken>((m, _) => sentMessage = m)
            .Returns(Task.CompletedTask);

        var result = await CreateSut().ProcessAsync(message);

        result.IsSuccess.Should().BeTrue();
        result.WasEnriched.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        sentMessage!["Name"]!.GetValue<string>().Should().Be("test");
    }

    [Fact]
    public async Task ProcessAsync_FetchFails_SendStillCalledOnce()
    {
        var message = JsonNode.Parse("""{"id":"123"}""")!;

        _apiClientMock
            .Setup(c => c.FetchEnrichmentDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));
        _apiClientMock
            .Setup(c => c.SendMessageAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateSut().ProcessAsync(message);

        _apiClientMock.Verify(
            c => c.SendMessageAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_OriginalFieldsPreserved()
    {
        var message = JsonNode.Parse("""{"id":"123","Name":"test","Email":"test@mail.ru"}""")!;

        _apiClientMock
            .Setup(c => c.FetchEnrichmentDataAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonNode.Parse("""{"age":25}""")!);
        _apiClientMock
            .Setup(c => c.SendMessageAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateSut().ProcessAsync(message);

        result.Payload["Name"]!.GetValue<string>().Should().Be("test");
        result.Payload["Email"]!.GetValue<string>().Should().Be("test@mail.ru");
    }

    [Fact]
    public async Task ProcessAsync_SendFails_ReturnsFailure()
    {
        var message = JsonNode.Parse("""{"id":"123"}""")!;

        _apiClientMock
            .Setup(c => c.FetchEnrichmentDataAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonNode.Parse("""{"age":25}""")!);
        _apiClientMock
            .Setup(c => c.SendMessageAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API down"));

        var result = await CreateSut().ProcessAsync(message);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("API down");
    }
}