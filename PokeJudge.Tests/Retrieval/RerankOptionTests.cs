namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Retrieval;

public class RerankOptionTests
{
    [Fact]
    public void Extract_NoFlag_DefaultsToJevAndReturnsArgsUnchanged()
    {
        var (remaining, rerank, error) = RerankOption.Extract(new[] { "--only", "notes" });

        Assert.Null(error);
        Assert.Equal(RerankOption.Jev, rerank);
        Assert.Equal(new[] { "--only", "notes" }, remaining);
    }

    [Fact]
    public void Extract_None_TurnsRerankingOffAndRemovesTheFlag()
    {
        var (remaining, rerank, error) = RerankOption.Extract(new[] { "--rerank", "none", "--repeat", "3" });

        Assert.Null(error);
        Assert.Null(rerank);
        Assert.Equal(new[] { "--repeat", "3" }, remaining);
    }

    [Fact]
    public void Extract_JevFlag_RemovesItAndReturnsJev()
    {
        var (remaining, rerank, error) = RerankOption.Extract(new[] { "--only", "notes", "--rerank", "jev", "--repeat", "3" });

        Assert.Null(error);
        Assert.Equal(RerankOption.Jev, rerank);
        Assert.Equal(new[] { "--only", "notes", "--repeat", "3" }, remaining);
    }

    [Fact]
    public void Extract_UnknownValue_ReturnsErrorNamingIt()
    {
        var (_, _, error) = RerankOption.Extract(new[] { "--rerank", "gemini" });

        Assert.Equal("Unknown reranker \"gemini\". Supported: jev, none.", error);
    }

    [Fact]
    public void Extract_MissingValue_ReturnsError()
    {
        var (_, _, error) = RerankOption.Extract(new[] { "--rerank" });

        Assert.Equal("\"--rerank\" requires a value.", error);
    }

    [Fact]
    public void Extract_FlagGivenTwice_ReturnsError()
    {
        var (_, _, error) = RerankOption.Extract(new[] { "--rerank", "none", "--rerank", "jev" });

        Assert.Equal("\"--rerank\" was given more than once.", error);
    }
}
