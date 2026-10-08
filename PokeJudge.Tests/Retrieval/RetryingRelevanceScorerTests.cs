namespace PokeJudge.Tests.Retrieval;

using System.Net;
using PokeJudge.Retrieval;
using PokeJudge.Tests.TestDoubles;

public class RetryingRelevanceScorerTests
{
    private static readonly IReadOnlyList<ScoredChunk> NoCandidates = Array.Empty<ScoredChunk>();

    private static HttpRequestException Http(HttpStatusCode? status) =>
        new($"Jev API request failed ({status})", null, status);

    private static (RetryingRelevanceScorer Scorer, StubRelevanceScorer Inner, List<TimeSpan> Delays, List<string> Log) Create()
    {
        var inner = new StubRelevanceScorer();
        var delays = new List<TimeSpan>();
        var log = new List<string>();
        var scorer = new RetryingRelevanceScorer(
            inner,
            onRetry: log.Add,
            delay: wait =>
            {
                delays.Add(wait);
                return Task.CompletedTask;
            });
        return (scorer, inner, delays, log);
    }

    [Fact]
    public async Task Success_IsReturnedWithoutWaiting()
    {
        var (scorer, inner, delays, _) = Create();
        inner.Enqueue(0.9, 0.1);

        var scores = await scorer.ScoreAsync("situation", NoCandidates);

        Assert.Equal(new[] { 0.9, 0.1 }, scores);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task ServiceUnavailable_IsRetriedUntilItSucceeds()
    {
        var (scorer, inner, _, _) = Create();
        inner.EnqueueFailure(Http(HttpStatusCode.ServiceUnavailable));
        inner.EnqueueFailure(Http(HttpStatusCode.ServiceUnavailable));
        inner.Enqueue(0.7);

        var scores = await scorer.ScoreAsync("situation", NoCandidates);

        Assert.Equal(new[] { 0.7 }, scores);
        Assert.Equal(3, inner.Situations.Count);
    }

    [Fact]
    public async Task Waits_DoubleFromOneSecond()
    {
        var (scorer, inner, delays, _) = Create();
        inner.EnqueueFailure(Http(HttpStatusCode.ServiceUnavailable));
        inner.EnqueueFailure(Http(HttpStatusCode.ServiceUnavailable));
        inner.EnqueueFailure(Http(HttpStatusCode.ServiceUnavailable));
        inner.Enqueue(0.7);

        await scorer.ScoreAsync("situation", NoCandidates);

        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }, delays);
    }

    [Fact]
    public async Task FourthFailure_IsRethrown()
    {
        var (scorer, inner, _, _) = Create();
        for (var i = 0; i < 3; i++)
        {
            inner.EnqueueFailure(Http(HttpStatusCode.ServiceUnavailable));
        }
        var last = Http(HttpStatusCode.BadGateway);
        inner.EnqueueFailure(last);

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => scorer.ScoreAsync("situation", NoCandidates));

        Assert.Same(last, thrown);
        Assert.Equal(4, inner.Situations.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(null)]
    public async Task TemporaryFailures_AreRetried(HttpStatusCode? status)
    {
        var (scorer, inner, _, _) = Create();
        inner.EnqueueFailure(Http(status));
        inner.Enqueue(0.5);

        var scores = await scorer.ScoreAsync("situation", NoCandidates);

        Assert.Equal(new[] { 0.5 }, scores);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ClientErrors_AreNotRetried(HttpStatusCode status)
    {
        var (scorer, inner, delays, _) = Create();
        inner.EnqueueFailure(Http(status));
        inner.Enqueue(0.5);

        await Assert.ThrowsAsync<HttpRequestException>(() => scorer.ScoreAsync("situation", NoCandidates));

        Assert.Single(inner.Situations);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task ParserFailures_AreNotRetried()
    {
        var (scorer, inner, _, _) = Create();
        inner.EnqueueFailure(new InvalidOperationException("unparseable"));
        inner.Enqueue(0.5);

        await Assert.ThrowsAsync<InvalidOperationException>(() => scorer.ScoreAsync("situation", NoCandidates));

        Assert.Single(inner.Situations);
    }

    [Fact]
    public async Task EachRetry_IsReported()
    {
        var (scorer, inner, _, log) = Create();
        inner.EnqueueFailure(Http(HttpStatusCode.ServiceUnavailable));
        inner.EnqueueFailure(Http(null));
        inner.Enqueue(0.5);

        await scorer.ScoreAsync("situation", NoCandidates);

        Assert.Equal(
            new[]
            {
                "Jev 503, retrying in 1 s (attempt 2 of 4)",
                "Jev network error, retrying in 2 s (attempt 3 of 4)",
            },
            log);
    }
}
