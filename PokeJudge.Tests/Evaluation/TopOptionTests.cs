namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Clarification;
using PokeJudge.Evaluation;

public class TopOptionTests
{
    [Fact]
    public void Extract_NoFlag_DefaultsToTheLoopsTopKAndReturnsArgsUnchanged()
    {
        var (remaining, topK, error) = TopOption.Extract(new[] { "--only", "notes" });

        Assert.Null(error);
        Assert.Equal(ClarificationLoop.DefaultTopK, topK);
        Assert.Equal(new[] { "--only", "notes" }, remaining);
    }

    [Fact]
    public void Extract_Value_RemovesTheFlagAndReturnsIt()
    {
        var (remaining, topK, error) = TopOption.Extract(new[] { "--repeat", "3", "--top", "10" });

        Assert.Null(error);
        Assert.Equal(10, topK);
        Assert.Equal(new[] { "--repeat", "3" }, remaining);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("ten")]
    public void Extract_NonPositiveOrNonNumber_ReturnsError(string value)
    {
        var (_, _, error) = TopOption.Extract(new[] { "--top", value });

        Assert.Equal($"\"--top\" requires a positive integer, got \"{value}\".", error);
    }

    [Fact]
    public void Extract_MissingValue_ReturnsError()
    {
        var (_, _, error) = TopOption.Extract(new[] { "--top" });

        Assert.Equal("\"--top\" requires a value.", error);
    }

    [Fact]
    public void Extract_GivenTwice_ReturnsError()
    {
        var (_, _, error) = TopOption.Extract(new[] { "--top", "5", "--top", "10" });

        Assert.Equal("\"--top\" was given more than once.", error);
    }
}
