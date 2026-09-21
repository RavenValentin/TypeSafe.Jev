using System.Text.Json.Nodes;
using static Jev.Net.Tests.Unit.Fixtures;

namespace Jev.Net.Tests.Unit;

public class RequestTests
{
    [Fact]
    public async Task Writes_the_documented_wire_format()
    {
        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await client.SystemOneAsync(JsonNode.Parse("""{"document":"hi"}"""), Questions);

        var request = handler.Requests.Single();
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://api.typesafe.ai/v1/systemone", request.Uri);
        Assert.Equal("Bearer test-key", request.Headers["Authorization"]);
        Assert.StartsWith("jev-net/", request.Headers["User-Agent"]);

        var json = JsonNode.Parse(request.Body)!;
        Assert.Equal("jev-latest", (string?)json["model"]);
        Assert.Equal("hi", (string?)json["state"]!["document"]);
        Assert.Equal("noul", (string?)json["questions"]!["urgent"]!["type"]);
        Assert.Null(json["questions"]!["urgent"]!["criteria"]);
        Assert.Equal(3, json["questions"]!["tone"]!["criteria"]!.AsArray().Count);
    }

    [Fact]
    public async Task Enum_options_carry_snake_case_names_and_Description_rubrics()
    {
        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await client.SystemOneAsync("text", Questions);

        var criteria = JsonNode.Parse(handler.Requests.Single().Body)!["questions"]!["category"]!["criteria"]!.AsObject();
        Assert.Equal("Charges, refunds, invoices", (string?)criteria["billing"]);
        Assert.Equal("Bugs and outages", (string?)criteria["technical_issue"]);
        Assert.True(criteria.ContainsKey("other"));
        Assert.Null(criteria["other"]);          // no [Description] is sent as an explicit null
    }

    [Fact]
    public async Task Noul_criteria_are_written_when_given()
    {
        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await client.SystemOneAsync("text", new Dictionary<string, Question>
        {
            ["complaint"] = Question.Noul("Is this a complaint?", whenTrue: "Wants a change", whenFalse: "Just reporting"),
        });

        var criteria = JsonNode.Parse(handler.Requests.Single().Body)!["questions"]!["complaint"]!["criteria"]!;
        Assert.Equal("Wants a change", (string?)criteria["true"]);
        Assert.Equal("Just reporting", (string?)criteria["false"]);
    }

    [Fact]
    public async Task The_same_JsonObject_can_be_used_in_two_questions_and_sent_twice()
    {
        // A JsonNode has a single parent, so a client that held them by reference would throw here.
        var shared = new JsonObject { ["what"] = "shared rubric" };
        var questions = new Dictionary<string, Question>
        {
            ["a"] = Question.Choice(shared, new Dictionary<string, JsonContent> { ["x"] = shared, ["y"] = shared }),
            ["b"] = Question.Choice(shared, new Dictionary<string, JsonContent> { ["x"] = shared }),
        };

        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await client.SystemOneAsync("text", questions);
        await client.SystemOneAsync("text", questions);

        Assert.Equal(2, handler.Requests.Count);
        var criteria = JsonNode.Parse(handler.Requests[1].Body)!["questions"]!["a"]!["criteria"]!;
        Assert.Equal("shared rubric", (string?)criteria["x"]!["what"]);
    }

    [Fact]
    public async Task Mutating_the_source_node_afterwards_does_not_change_what_is_sent()
    {
        var node = new JsonObject { ["level"] = "one" };
        var question = Question.Noul(node);
        node["level"] = "two";

        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await client.SystemOneAsync("s", new Dictionary<string, Question> { ["q"] = question });

        var sent = JsonNode.Parse(handler.Requests.Single().Body)!["questions"]!["q"]!["instructions"]!;
        Assert.Equal("one", (string?)sent["level"]);
    }

    [Fact]
    public async Task Raw_questions_are_sent_verbatim()
    {
        var raw = new JsonObject
        {
            ["type"] = "noul",
            ["instructions"] = "Something new",
            ["future_field"] = 42,
        };

        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await client.SystemOneAsync("s", new Dictionary<string, Question> { ["q"] = raw });

        var sent = JsonNode.Parse(handler.Requests.Single().Body)!["questions"]!["q"]!;
        Assert.Equal(42, (int?)sent["future_field"]);
        Assert.Equal("noul", (string?)sent["type"]);
    }

    [Theory]
    [InlineData("""{"instructions":"no type"}""")]
    [InlineData("""{"type":"choice"}""")]
    [InlineData("""{"type":"score","criteria":[]}""")]
    public void Raw_questions_are_checked_for_structure(string json)
    {
        Assert.Throws<ArgumentException>(() => Question.Raw(JsonNode.Parse(json)!.AsObject()));
    }

    [Fact]
    public void Empty_criteria_are_rejected_before_the_network()
    {
        Assert.Throws<ArgumentException>(() =>
            new ChoiceQuestion { Instructions = "x", Criteria = new Dictionary<string, JsonContent>() });
        Assert.Throws<ArgumentException>(() =>
            new ScoreQuestion { Instructions = "x", Criteria = [] });
    }

    [Fact]
    public async Task Empty_question_sets_are_rejected_before_the_network()
    {
        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.SystemOneAsync("s", new Dictionary<string, Question>()));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Extra_body_and_headers_are_merged_but_cannot_overwrite_what_the_client_owns()
    {
        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await client.SystemOneAsync("s", Questions, new JevRequestOptions
        {
            Model = "jev-1.13.0",
            ExtraHeaders = new Dictionary<string, string> { ["X-Trace"] = "abc" },
            ExtraBody = new Dictionary<string, JsonNode?> { ["experimental"] = true },
        });

        var request = handler.Requests.Single();
        Assert.Equal("abc", request.Headers["X-Trace"]);
        var json = JsonNode.Parse(request.Body)!;
        Assert.Equal("jev-1.13.0", (string?)json["model"]);
        Assert.True((bool?)json["experimental"]);

        await Assert.ThrowsAsync<ArgumentException>(() => client.SystemOneAsync("s", Questions, new JevRequestOptions
        {
            ExtraBody = new Dictionary<string, JsonNode?> { ["model"] = "sneaky" },
        }));

        Assert.Throws<JevException>(() => Client(handler, new JevClientOptions
        {
            ApiKey = "k",
            DefaultHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer other" },
        }));
    }

    [Fact]
    public async Task Null_state_is_sent_explicitly()
    {
        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await client.SystemOneAsync(default, Questions);

        Assert.Contains("\"state\":null", handler.Requests.Single().Body);
    }
}
