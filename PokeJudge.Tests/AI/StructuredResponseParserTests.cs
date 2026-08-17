namespace PokeJudge.Tests.AI;

using System.Text.Json;
using PokeJudge.AI;
using PokeJudge.Grounding;
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
                { "question": "Was a Pokemon Knocked Out?", "relatedChunkId": "A1#0" }
              ]
            }
            """;

        var result = StructuredResponseParser.Parse<ClarificationResult>(json);

        Assert.False(result.IsSufficient);
        Assert.Single(result.Questions);
        Assert.Equal("Was a Pokemon Knocked Out?", result.Questions[0].Question);
        Assert.Equal("A1#0", result.Questions[0].RelatedChunkId);
    }

    [Fact]
    public void Parse_ClarificationResultJson_Sufficient_DeserializesCorrectly()
    {
        const string json = """
            {
              "isSufficient": true,
              "questions": []
            }
            """;

        var result = StructuredResponseParser.Parse<ClarificationResult>(json);

        Assert.True(result.IsSufficient);
        Assert.Empty(result.Questions);
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
    public void Parse_RulingResultJson_DeserializesSourceSupportEnumFromString()
    {
        const string json = """
            {
              "recommendation": "Issue a Warning.",
              "explanation": "The error was discovered after the player's next turn ended.",
              "repairSteps": ["Correct the Prize count."],
              "penaltyGuidance": "Procedural Error -- Warning.",
              "citedChunkIds": ["A1#0", "A2#0"],
              "sourceSupport": "Strong",
              "sourceSupportRationale": "Both retrieved passages directly address the discovery-timing rule."
            }
            """;

        var result = StructuredResponseParser.Parse<RulingResult>(json);

        Assert.Equal("Issue a Warning.", result.Recommendation);
        Assert.Equal(new[] { "Correct the Prize count." }, result.RepairSteps);
        Assert.Equal(new[] { "A1#0", "A2#0" }, result.CitedChunkIds);
        Assert.Equal(SourceSupport.Strong, result.SourceSupport);
    }

    [Fact]
    public void Parse_RulingResultJson_NullPenaltyGuidance_DeserializesToNull()
    {
        const string json = """
            {
              "recommendation": "No action needed.",
              "explanation": "No rule was violated.",
              "repairSteps": [],
              "penaltyGuidance": null,
              "citedChunkIds": ["A1#0"],
              "sourceSupport": "Partial",
              "sourceSupportRationale": "Only one of two relevant passages was retrieved."
            }
            """;

        var result = StructuredResponseParser.Parse<RulingResult>(json);

        Assert.Null(result.PenaltyGuidance);
        Assert.Equal(SourceSupport.Partial, result.SourceSupport);
    }

    [Fact]
    public void Parse_GroundingAssessmentJson_DeserializesCitationSupportLevelEnumsAndConflictFlag()
    {
        const string json = """
            {
              "citations": [
                { "chunkId": "A1#0", "supportLevel": "ExplicitSupport" },
                { "chunkId": "A2#0", "supportLevel": "Interpretation" }
              ],
              "conflictDetected": true,
              "rationale": "One citation requires judge discretion; two passages conflict."
            }
            """;

        var result = StructuredResponseParser.Parse<GroundingAssessment>(json);

        Assert.Equal(2, result.Citations.Count);
        Assert.Equal("A1#0", result.Citations[0].ChunkId);
        Assert.Equal(CitationSupportLevel.ExplicitSupport, result.Citations[0].SupportLevel);
        Assert.Equal(CitationSupportLevel.Interpretation, result.Citations[1].SupportLevel);
        Assert.True(result.ConflictDetected);
    }

    [Fact]
    public void Parse_GroundingAssessmentJson_UnsupportedCitationAndNoConflict_DeserializesCorrectly()
    {
        const string json = """
            {
              "citations": [
                { "chunkId": "A1#0", "supportLevel": "Unsupported" }
              ],
              "conflictDetected": false,
              "rationale": "The cited passage does not address the claim."
            }
            """;

        var result = StructuredResponseParser.Parse<GroundingAssessment>(json);

        Assert.Equal(CitationSupportLevel.Unsupported, result.Citations[0].SupportLevel);
        Assert.False(result.ConflictDetected);
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
