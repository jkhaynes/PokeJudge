namespace PokeJudge.AI;

using System.Text.Json;

// Spaces calls to an inner client evenly so a run stays under a requests-per-minute
// quota (Gemini's free tier allows 15 generateContent requests per minute). It waits
// before a call rather than retrying after a 429, so no request is wasted and no
// scenario becomes an infrastructure failure just because the harness ran too fast.
// Opt-in via the Gemini:RequestsPerMinute user secret; only `evaluate` uses it, since
// the judge-facing flow is already paced by a human typing.
public sealed class PacedLlmClient : ILlmClient
{
    private readonly ILlmClient _inner;
    private readonly TimeSpan _interval;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<TimeSpan, Task> _delay;
    private DateTimeOffset? _lastCallStartedAt;

    public PacedLlmClient(
        ILlmClient inner,
        int requestsPerMinute,
        Func<DateTimeOffset>? now = null,
        Func<TimeSpan, Task>? delay = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requestsPerMinute, 1);

        _inner = inner;
        _interval = TimeSpan.FromMinutes(1) / requestsPerMinute;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _delay = delay ?? (wait => Task.Delay(wait));
    }

    // ponytail: not thread-safe; the eval harness awaits one call at a time. Add a
    // SemaphoreSlim around the wait if scenarios ever run concurrently.
    public async Task<T> CompleteStructuredAsync<T>(string systemInstruction, string userContent, JsonElement responseSchema)
    {
        if (_lastCallStartedAt is { } lastCallStartedAt)
        {
            var wait = lastCallStartedAt + _interval - _now();
            if (wait > TimeSpan.Zero)
            {
                await _delay(wait);
            }
        }

        _lastCallStartedAt = _now();
        return await _inner.CompleteStructuredAsync<T>(systemInstruction, userContent, responseSchema);
    }
}
