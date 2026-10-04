namespace PokeJudge.Tests.AI;

using PokeJudge.AI;

public class JevResponseParserTests
{
    [Fact]
    public void Parse_ObjectAnswers_ReturnsProbabilitiesInKeyOrder()
    {
        const string json = """
            { "model": "jev-1.13.0",
              "p1": { "probability": 0.12 },
              "p0": { "probability": 0.91 },
              "usage": { "input_tokens": 900, "output_tokens": 0 } }
            """;

        var scores = JevResponseParser.Parse(json, new[] { "p0", "p1" });

        Assert.Equal(new[] { 0.91, 0.12 }, scores);
    }

    [Fact]
    public void Parse_BareNumberAnswers_AreAccepted()
    {
        const string json = """{ "model": "jev-1.13.0", "p0": 0.4 }""";

        Assert.Equal(new[] { 0.4 }, JevResponseParser.Parse(json, new[] { "p0" }));
    }

    [Fact]
    public void Parse_MissingKey_ThrowsNamingIt()
    {
        const string json = """{ "model": "jev-1.13.0", "p0": { "probability": 0.4 } }""";

        var ex = Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0", "p1" }));

        Assert.Contains("\"p1\"", ex.Message);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public void Parse_ProbabilityOutsideZeroToOne_Throws(string value)
    {
        var json = $$"""{ "p0": { "probability": {{value}} } }""";

        var ex = Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0" }));

        Assert.Contains("p0", ex.Message);
    }

    [Fact]
    public void Parse_AnswerWithoutAProbability_Throws()
    {
        const string json = """{ "p0": { "choice": "yes" } }""";

        Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0" }));
    }
}
