namespace PokeJudge.Tests;

public class NaiveSufficiencyParserTests
{
    [Fact]
    public void Parse_ClearSufficientAnswer_ReturnsSufficient()
    {
        var result = NaiveSufficiencyParser.Parse(
            "Yes, this is clear enough to rule on.");

        Assert.Equal("SUFFICIENT", result);
    }

    [Fact]
    public void Parse_ClearInsufficientAnswer_ReturnsInsufficient()
    {
        var result = NaiveSufficiencyParser.Parse(
            "More details are needed before a ruling can be made.");

        Assert.Equal("INSUFFICIENT", result);
    }

    [Fact]
    public void Parse_UnrelatedText_ReturnsUnknown()
    {
        var result = NaiveSufficiencyParser.Parse(
            "The weather at the venue was pleasant today.");

        Assert.Equal("UNKNOWN (neither pattern matched)", result);
    }

    // Milestone 1's whole point: naive substring matching can't tell "sufficient"
    // from "not sufficient". This test locks in that observed failure as a
    // permanent artifact rather than letting it silently disappear if someone
    // "fixes" the regex before Milestone 2 introduces structured output.
    [Fact]
    public void Parse_NegatedSufficient_IsMisclassifiedAsSufficient()
    {
        var result = NaiveSufficiencyParser.Parse(
            "The information given is not sufficient to make a ruling.");

        Assert.Equal("SUFFICIENT", result);
    }

    [Fact]
    public void Parse_NegatedSufficientAlongsideInsufficientPhrase_ReturnsAmbiguous()
    {
        var result = NaiveSufficiencyParser.Parse(
            "The information given is not sufficient, as more details are needed.");

        Assert.Equal("AMBIGUOUS (both patterns matched)", result);
    }
}
