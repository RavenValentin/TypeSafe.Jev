using System.Net;
using System.Text.Json.Serialization;
using static Jev.Net.Tests.Unit.Fixtures;

namespace Jev.Net.Tests.Unit;

public class ResponseTests
{
    [Fact]
    public async Task Parses_all_three_answer_types()
    {
        using var client = Client(new StubHandler(Ok(("x-typesafe-request-id", "req-1"))));

        var result = await client.SystemOneAsync("text", Questions);

        Assert.Equal("jev-1.13.0", result.Model);
        Assert.Equal("req-1", result.RequestId);
        Assert.Equal(392, result.Usage!.InputTokens);
        Assert.Equal(0.98, result.Noul("urgent").Noul);

        var category = result.Choice("category");
        Assert.Equal(Category.TechnicalIssue, category.As<Category>());
        Assert.Equal(0.9, category.Confidence);
        Assert.Equal(0.9, category.Probabilities["technical_issue"]);

        var tone = result.Score("tone");
        Assert.Equal(1.7, tone.Score);
        Assert.Equal("angry", (string?)tone.Legend[2]);
        Assert.Equal(0.8, tone.Probabilities[2]);
    }

    [Fact]
    public async Task Asking_for_the_wrong_kind_or_an_unknown_name_names_the_field()
    {
        using var client = Client(new StubHandler(Ok()));
        var result = await client.SystemOneAsync("text", Questions);

        var wrongKind = Assert.Throws<JevResponseValidationException>(() => result.Noul("category"));
        Assert.Equal("answers.category", wrongKind.FieldPath);

        var missing = Assert.Throws<JevResponseValidationException>(() => result.Noul("nope"));
        Assert.Equal("answers.nope", missing.FieldPath);
    }

    [Fact]
    public async Task A_choice_outside_the_enum_is_a_validation_error()
    {
        const string body = """
            {"model":"m","answers":{"category":{"type":"choice","choice":"martian","confidence":1,"probabilities":{"martian":1}}}}
            """;
        using var client = Client(new StubHandler(StubHandler.Json(HttpStatusCode.OK, body)));

        var result = await client.SystemOneAsync("text", Questions);

        Assert.Throws<JevResponseValidationException>(() => result.Choice("category").As<Category>());
    }

    [Theory]
    [InlineData("""{"model":"m","answers":{"q":{"type":"weird"}}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"noul":0.5}}}""")]
    [InlineData("not json at all")]
    public async Task A_malformed_body_is_a_validation_error(string body)
    {
        using var client = Client(new StubHandler(StubHandler.Json(HttpStatusCode.OK, body)));

        await Assert.ThrowsAsync<JevResponseValidationException>(() => client.SystemOneAsync("s", Questions));
    }

    private sealed record Ticket : SystemOneResponse
    {
        public NoulAnswer Urgent { get; set; } = null!;
        public ChoiceAnswer Category { get; set; } = null!;
        [JsonPropertyName("tone")] public ScoreAnswer Mood { get; set; } = null!;
        [OptionalAnswer] public NoulAnswer? Spam { get; set; }
    }

    [Fact]
    public async Task Typed_responses_are_filled_by_name()
    {
        using var client = Client(new StubHandler(Ok(("x-typesafe-request-id", "r"))));

        var ticket = await client.SystemOneAsync<Ticket>("text", Questions);

        Assert.Equal(0.98, ticket.Urgent.Noul);
        Assert.Equal(Category.TechnicalIssue, ticket.Category.As<Category>());
        Assert.Equal(1.7, ticket.Mood.Score);          // matched through [JsonPropertyName]
        Assert.Null(ticket.Spam);                       // [OptionalAnswer], and absent
        Assert.Equal("jev-1.13.0", ticket.Model);
        Assert.Equal("r", ticket.RequestId);
        Assert.Equal(3, ticket.Answers.Count);
    }

    private sealed record Demanding : SystemOneResponse
    {
        public NoulAnswer Missing { get; set; } = null!;
    }

    [Fact]
    public async Task A_required_answer_property_with_no_answer_is_an_error()
    {
        using var client = Client(new StubHandler(Ok()));

        var error = await Assert.ThrowsAsync<JevResponseValidationException>(
            () => client.SystemOneAsync<Demanding>("text", Questions));

        Assert.Equal("answers.Missing", error.FieldPath);
    }

    private sealed record Mistyped : SystemOneResponse
    {
        public NoulAnswer Category { get; set; } = null!;
    }

    [Fact]
    public async Task An_answer_property_of_the_wrong_kind_is_an_error()
    {
        using var client = Client(new StubHandler(Ok()));

        await Assert.ThrowsAsync<JevResponseValidationException>(
            () => client.SystemOneAsync<Mistyped>("text", Questions));
    }

    [Fact]
    public async Task Models_are_read_from_a_bare_array_or_a_wrapper()
    {
        const string bare = """[{"name":"jev-latest","description":"alias","release_date":"2026-05-01"}]""";
        using var client = Client(new StubHandler(StubHandler.Json(HttpStatusCode.OK, bare)));
        var models = await client.ListModelsAsync();
        Assert.Equal("jev-latest", models.Single().Name);
        Assert.Equal("2026-05-01", models.Single().ReleaseDate);

        const string wrapped = """{"data":[{"name":"jev-1.13.0"}]}""";
        using var other = Client(new StubHandler(StubHandler.Json(HttpStatusCode.OK, wrapped)));
        Assert.Equal("jev-1.13.0", (await other.ListModelsAsync()).Single().Name);
    }
}
