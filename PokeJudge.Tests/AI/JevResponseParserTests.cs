namespace PokeJudge.Tests.AI;

using PokeJudge.AI;

public class JevResponseParserTests
{
    // Copied from a live /v1/systemone response (2026-10-04), with p0 and p1 swapped in
    // the answers object to prove the parser reads by key, not by position.
    private const string LiveResponse = """
        {"model":"jev-1.13.0","answers":{"p1":{"type":"noul","noul":0.06},"p0":{"type":"noul","noul":0.27}},"usage":{"input_tokens":449,"output_tokens":38}}
        """;

    [Fact]
    public void Parse_LiveResponse_ReturnsProbabilitiesInKeyOrder()
    {
        Assert.Equal(new[] { 0.27, 0.06 }, JevResponseParser.Parse(LiveResponse, new[] { "p0", "p1" }));
    }

    [Fact]
    public void Parse_NoAnswersObject_Throws()
    {
        const string json = """{ "model": "jev-1.13.0", "p0": { "type": "noul", "noul": 0.4 } }""";

        var ex = Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0" }));

        Assert.Contains("answers", ex.Message);
    }

    [Fact]
    public void Parse_MissingKey_ThrowsNamingIt()
    {
        const string json = """{ "answers": { "p0": { "type": "noul", "noul": 0.4 } } }""";

        var ex = Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0", "p1" }));

        Assert.Contains("\"p1\"", ex.Message);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public void Parse_ProbabilityOutsideZeroToOne_Throws(string value)
    {
        var json = $$"""{ "answers": { "p0": { "type": "noul", "noul": {{value}} } } }""";

        var ex = Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0" }));

        Assert.Contains("p0", ex.Message);
    }

    [Fact]
    public void Parse_AnswerWithoutANoulProbability_Throws()
    {
        const string json = """{ "answers": { "p0": { "type": "choice", "choice": "yes" } } }""";

        Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0" }));
    }
}
