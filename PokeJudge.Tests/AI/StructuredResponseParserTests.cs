namespace PokeJudge.Tests.AI;

using System.Text.Json;
using PokeJudge.AI;
using PokeJudge.StructuredState;

public class StructuredResponseParserTests
{
    [Fact]
    public void Parse_ClarificationResultJson_InsufficientWithQuestions_DeserializesCorrectly()
    {
        const string json = """
            {
              "isSufficient": false,
              "questions": [
                { "question": "Was a Pokemon Knocked Out?", "relatedSnippetId": "A1" }
              ],
              "draft": null
            }
            """;

        var result = StructuredResponseParser.Parse<ClarificationResult>(json);

        Assert.False(result.IsSufficient);
        Assert.Single(result.Questions);
        Assert.Equal("Was a Pokemon Knocked Out?", result.Questions[0].Question);
        Assert.Equal("A1", result.Questions[0].RelatedSnippetId);
        Assert.Null(result.Draft);
    }

    [Fact]
    public void Parse_ClarificationResultJson_SufficientWithDraft_DeserializesCorrectly()
    {
        const string json = """
            {
              "isSufficient": true,
              "questions": [],
              "draft": {
                "recommendedAction": "Issue a Warning.",
                "supportingSnippetIds": ["A1", "A2"]
              }
            }
            """;

        var result = StructuredResponseParser.Parse<ClarificationResult>(json);

        Assert.True(result.IsSufficient);
        Assert.Empty(result.Questions);
        Assert.NotNull(result.Draft);
        Assert.Equal("Issue a Warning.", result.Draft!.RecommendedAction);
        Assert.Equal(new[] { "A1", "A2" }, result.Draft.SupportingSnippetIds);
    }

    [Fact]
    public void Parse_FactExtractionResultJson_DeserializesConfirmedFactsAndHypotheses()
    {
        const string json = """
            {
              "confirmedFacts": ["A Pokemon was Knocked Out."],
              "hypotheses": ["The player likely forgot due to time pressure."]
            }
            """;

        var result = StructuredResponseParser.Parse<FactExtractionResult>(json);

        Assert.Equal(new[] { "A Pokemon was Knocked Out." }, result.ConfirmedFacts);
        Assert.Equal(new[] { "The player likely forgot due to time pressure." }, result.Hypotheses);
    }

    [Fact]
    public void Parse_CamelCaseJsonKeys_MatchPascalCaseRecordProperties()
    {
        // Gemini's schema uses camelCase keys; the C# records use PascalCase.
        // Case-insensitive matching is what bridges the two -- lock that in.
        const string json = """{ "confirmedFacts": [], "hypotheses": [] }""";

        var result = StructuredResponseParser.Parse<FactExtractionResult>(json);

        Assert.Empty(result.ConfirmedFacts);
        Assert.Empty(result.Hypotheses);
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsRatherThanSilentlyReturningEmptyResult()
    {
        // Milestone 1 showed naive parsing fails silently/inconsistently.
        // Structured parsing should fail LOUDLY when the model doesn't
        // conform, per the PRD's "fail visibly" requirement.
        const string malformed = "{ this is not valid json";

        Assert.Throws<JsonException>(() => StructuredResponseParser.Parse<FactExtractionResult>(malformed));
    }
}
