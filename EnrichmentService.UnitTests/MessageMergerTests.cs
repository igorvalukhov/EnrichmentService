using EnrichmentService.Services;
using FluentAssertions;
using System.Text.Json.Nodes;

namespace EnrichmentService.UnitTests;

/// <summary>
/// Unit тесты для MessageMerger.
/// </summary>
public sealed class MessageMergerTests
{
    private readonly MessageMerger _sut = new(new JsonPathAccessor());

    /// <summary>
    /// Без данных обогащения возвращается идентичная копия оригинала.
    /// </summary>
    [Fact]
    public void Merge_NoEnrichmentData_ReturnsIdenticalCopy()
    {
        var original = JsonNode.Parse("""{"id":1,"Name":"test"}""")!;

        var result = _sut.Merge(original, new Dictionary<string, JsonNode>());

        result["id"]!.GetValue<int>().Should().Be(1);
        result["Name"]!.GetValue<string>().Should().Be("test");
    }

    /// <summary>
    /// Данные обогащения записываются по указанному DestinationPath.
    /// </summary>
    [Fact]
    public void Merge_AddsEnrichmentAtDestinationPath()
    {
        var original = JsonNode.Parse("""{"id":1,"Name":"test"}""")!;
        var enrichment = new Dictionary<string, JsonNode>
        {
            ["userDetails"] = JsonNode.Parse("""{"age":25,"city":"Moscow"}""")!
        };

        var result = _sut.Merge(original, enrichment);

        result["userDetails"]!["age"]!.GetValue<int>().Should().Be(25);
        result["userDetails"]!["city"]!.GetValue<string>().Should().Be("Moscow");
    }

    /// <summary>
    /// Оригинальный объект не изменяется после слияния.
    /// </summary>
    [Fact]
    public void Merge_DoesNotMutateOriginal()
    {
        var original = JsonNode.Parse("""{"id":1}""")!;
        var enrichment = new Dictionary<string, JsonNode>
        {
            ["userDetails"] = JsonNode.Parse("""{"age":25}""")!
        };

        _ = _sut.Merge(original, enrichment);

        original.AsObject().ContainsKey("userDetails").Should().BeFalse();
    }

    /// <summary>
    /// Все поля оригинала сохраняются в результате слияния.
    /// </summary>
    [Fact]
    public void Merge_PreservesAllOriginalFields()
    {
        var original = JsonNode.Parse("""
        {
            "id": 123,
            "Name": "test",
            "Email": "test@mail.ru"
        }
        """)!;

        var result = _sut.Merge(original, new Dictionary<string, JsonNode>
        {
            ["userDetails"] = JsonNode.Parse("""{"age":25}""")!
        });

        result["id"]!.GetValue<int>().Should().Be(123);
        result["Name"]!.GetValue<string>().Should().Be("test");
        result["Email"]!.GetValue<string>().Should().Be("test@mail.ru");
    }

    /// <summary>
    /// Несколько правил обогащения, все применяются.
    /// </summary>
    [Fact]
    public void Merge_MultipleRules_AllApplied()
    {
        var original = JsonNode.Parse("""{"id":1,"cityId":77}""")!;
        var enrichment = new Dictionary<string, JsonNode>
        {
            ["userDetails"] = JsonNode.Parse("""{"age":25}""")!,
            ["cityDetails"] = JsonNode.Parse("""{"name":"Moscow"}""")!
        };

        var result = _sut.Merge(original, enrichment);

        result["userDetails"]!["age"]!.GetValue<int>().Should().Be(25);
        result["cityDetails"]!["name"]!.GetValue<string>().Should().Be("Moscow");
    }

    /// <summary>
    /// Null оригинал, выбрасывается ArgumentNullException.
    /// </summary>
    [Fact]
    public void Merge_NullOriginal_Throws()
    {
        var act = () => _sut.Merge(null!, new Dictionary<string, JsonNode>());
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Корневой элемент не является объектом, выбрасывается InvalidOperationException.
    /// </summary>
    [Fact]
    public void Merge_ArrayRoot_Throws()
    {
        var array = JsonNode.Parse("""[1,2,3]""")!;
        var act = () => _sut.Merge(array, new Dictionary<string, JsonNode>());
        act.Should().Throw<InvalidOperationException>().WithMessage("*JsonObject*");
    }
}