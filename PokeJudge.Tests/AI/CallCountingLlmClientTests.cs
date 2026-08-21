namespace PokeJudge.Tests.AI;

using PokeJudge.AI;
using PokeJudge.Tests.TestDoubles;

public class CallCountingLlmClientTests
{
    [Fact]
    public async Task CompleteStructuredAsync_StartsAtZero()
    {
        var inner = new StubLlmClient();
        var counting = new CallCountingLlmClient(inner);

        Assert.Equal(0, counting.CallCount);
    }

    [Fact]
    public async Task CompleteStructuredAsync_EachCall_IncrementsCallCount()
    {
        var inner = new StubLlmClient();
        inner.Enqueue("result-1");
        inner.Enqueue("result-2");
        var counting = new CallCountingLlmClient(inner);

        await counting.CompleteStructuredAsync<string>("sys", "user", default);
        Assert.Equal(1, counting.CallCount);

        await counting.CompleteStructuredAsync<string>("sys", "user", default);
        Assert.Equal(2, counting.CallCount);
    }

    [Fact]
    public async Task CompleteStructuredAsync_DelegatesToInnerClientAndReturnsItsResult()
    {
        var inner = new StubLlmClient();
        inner.Enqueue("the real result");
        var counting = new CallCountingLlmClient(inner);

        var result = await counting.CompleteStructuredAsync<string>("sys", "user", default);

        Assert.Equal("the real result", result);
    }

    [Fact]
    public async Task CompleteStructuredAsync_InnerClientThrows_StillIncrementsCallCountBeforePropagating()
    {
        // A request that was attempted and rejected (e.g. a 429) still consumed a slot against
        // the per-minute quota -- pacing must count it, not just successful calls, or it would
        // undercount real usage and pace too aggressively.
        var inner = new StubLlmClient();
        var counting = new CallCountingLlmClient(inner);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => counting.CompleteStructuredAsync<string>("sys", "user", default));

        Assert.Equal(1, counting.CallCount);
    }
}
