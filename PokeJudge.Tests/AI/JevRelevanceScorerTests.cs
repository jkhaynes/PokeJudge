namespace PokeJudge.Tests.AI;

using System.Text.Json;
using PokeJudge.AI;
using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;

public class JevRelevanceScorerTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string id, string text) =>
        new(new EmbeddedChunk(new TextChunk(id, id.Split('#')[0], text, Source), new[] { 1f }), 0.8);

    private static JsonElement Body() =>
        // Same options JsonContent.Create uses when the client sends the request.
        JsonSerializer.SerializeToElement(
            JevRelevanceScorer.BuildRequestBody(
                "jev-latest",
                "Player drew an extra card.",
                new[] { Chunk("PPG-5.5.1#0", "Drawing extra cards..."), Chunk("PPG-4.2.1#1", "Supporter cards...") }),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public void BuildRequestBody_SendsTheModel()
    {
        Assert.Equal("jev-latest", Body().GetProperty("model").GetString());
    }

    [Fact]
    public void BuildRequestBody_StateHoldsTheSituationAndEveryPassage()
    {
        var state = Body().GetProperty("state");

        Assert.Equal("Player drew an extra card.", state.GetProperty("situation").GetString());
        var passages = state.GetProperty("passages").EnumerateArray().ToList();
        Assert.Equal(new[] { "p0", "p1" }, passages.Select(p => p.GetProperty("id").GetString()));
        Assert.Equal("PPG-5.5.1#0", passages[0].GetProperty("source").GetString());
        Assert.Equal("Supporter cards...", passages[1].GetProperty("text").GetString());
    }

    [Fact]
    public void BuildRequestBody_AsksOneNoulQuestionPerPassage()
    {
        var questions = Body().GetProperty("questions").EnumerateObject().ToList();

        Assert.Equal(new[] { "p0", "p1" }, questions.Select(q => q.Name));
        Assert.All(questions, q => Assert.Equal("noul", q.Value.GetProperty("type").GetString()));
        Assert.Contains("p1", questions[1].Value.GetProperty("instructions").GetString());
    }

    [Fact]
    public void QuestionKeys_AreZeroBasedAndPrefixed()
    {
        Assert.Equal(new[] { "p0", "p1", "p2" }, JevRelevanceScorer.QuestionKeys(3));
    }

    [Fact]
    public void RequestFailed_CarriesTheStatusCodeAndBody()
    {
        var failure = JevRelevanceScorer.RequestFailed(System.Net.HttpStatusCode.ServiceUnavailable, "{\"detail\":\"model_unavailable\"}");

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        Assert.Equal("Jev API request failed (503 ServiceUnavailable): {\"detail\":\"model_unavailable\"}", failure.Message);
    }
}
