namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Retrieval;

public class SearchArgsParserTests
{
    [Fact]
    public void Parse_QueryOnly_DefaultsToTopFive()
    {
        var (args, error) = SearchArgsParser.Parse(new[] { "deck", "under", "60" });

        Assert.Null(error);
        Assert.Equal("deck under 60", args!.Query);
        Assert.Equal(5, args.TopK);
    }

    [Fact]
    public void Parse_TopFlag_SetsTopKAndIsNotPartOfTheQuery()
    {
        var (args, error) = SearchArgsParser.Parse(new[] { "--top", "30", "deck", "under", "60" });

        Assert.Null(error);
        Assert.Equal("deck under 60", args!.Query);
        Assert.Equal(30, args.TopK);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("abc")]
    public void Parse_TopNotAPositiveInteger_ReturnsError(string value)
    {
        var (args, error) = SearchArgsParser.Parse(new[] { "--top", value, "query" });

        Assert.Null(args);
        Assert.Equal($"\"--top\" requires a positive integer, got \"{value}\".", error);
    }

    [Fact]
    public void Parse_TopWithoutValue_ReturnsError()
    {
        var (_, error) = SearchArgsParser.Parse(new[] { "query", "--top" });

        Assert.Equal("\"--top\" requires a value.", error);
    }

    [Fact]
    public void Parse_NoQueryText_ReturnsUsage()
    {
        var (_, error) = SearchArgsParser.Parse(new[] { "--top", "10" });

        Assert.Equal(SearchArgsParser.Usage, error);
    }
}
