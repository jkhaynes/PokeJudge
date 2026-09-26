namespace PokeJudge.Tests.AI;

using System.Text.Json;
using PokeJudge.AI;
using PokeJudge.Tests.TestDoubles;

public class PacedLlmClientTests
{
    private static readonly JsonElement Schema = JsonDocument.Parse("{}").RootElement;

    // Fake clock: every requested delay advances time instead of sleeping.
    private sealed class FakeTime
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        public List<TimeSpan> Delays { get; } = new();

        public Task Delay(TimeSpan wait)
        {
            Delays.Add(wait);
            Now += wait;
            return Task.CompletedTask;
        }
    }

    private static (PacedLlmClient Client, StubLlmClient Inner, FakeTime Time) Create(int requestsPerMinute, int queuedResults)
    {
        var inner = new StubLlmClient();
        for (var i = 0; i < queuedResults; i++)
        {
            inner.Enqueue($"result {i}");
        }

        var time = new FakeTime();
        var client = new PacedLlmClient(inner, requestsPerMinute, () => time.Now, time.Delay);
        return (client, inner, time);
    }

    [Fact]
    public async Task FirstCall_DoesNotWait()
    {
        var (client, _, time) = Create(requestsPerMinute: 15, queuedResults: 1);

        var result = await client.CompleteStructuredAsync<string>("system", "user", Schema);

        Assert.Equal("result 0", result);
        Assert.Empty(time.Delays);
    }

    [Fact]
    public async Task BackToBackCalls_AreSpacedEvenlyAcrossTheMinute()
    {
        var (client, inner, time) = Create(requestsPerMinute: 15, queuedResults: 3);

        await client.CompleteStructuredAsync<string>("system", "first", Schema);
        await client.CompleteStructuredAsync<string>("system", "second", Schema);
        await client.CompleteStructuredAsync<string>("system", "third", Schema);

        Assert.Equal(new[] { TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4) }, time.Delays);
        Assert.Equal(new[] { "first", "second", "third" }, inner.UserContents);
    }

    [Fact]
    public async Task TimeAlreadySpentSinceTheLastCall_CountsTowardTheWait()
    {
        var (client, _, time) = Create(requestsPerMinute: 15, queuedResults: 3);

        await client.CompleteStructuredAsync<string>("system", "user", Schema);
        time.Now += TimeSpan.FromSeconds(3);
        await client.CompleteStructuredAsync<string>("system", "user", Schema);
        time.Now += TimeSpan.FromSeconds(10);
        await client.CompleteStructuredAsync<string>("system", "user", Schema);

        Assert.Equal(new[] { TimeSpan.FromSeconds(1) }, time.Delays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveRate_IsRejected(int requestsPerMinute)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PacedLlmClient(new StubLlmClient(), requestsPerMinute));
    }
}
