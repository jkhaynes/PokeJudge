namespace PokeJudge.Retrieval;

using System.Net;

// Retries temporary scorer failures: any 5xx, 429, or a network error with no status
// code. Jev answered 503 model_unavailable on 13 of 60 scenario-runs on 2026-10-07, in
// short bursts, so a few seconds of backoff usually rides one out. Other 4xx (bad key,
// bad request) and parser failures throw at once, because retrying can't fix them.
// After the last attempt the failure is rethrown, so `evaluate` still counts it as an
// infrastructure failure; it never falls back to cosine order, which would mix cosine
// results into Jev measurements. Same injectable-delay shape as PacedLlmClient.
public sealed class RetryingRelevanceScorer : IRelevanceScorer
{
    internal const int MaxAttempts = 4;
    private static readonly TimeSpan FirstWait = TimeSpan.FromSeconds(1);

    private readonly IRelevanceScorer _inner;
    private readonly Action<string>? _onRetry;
    private readonly Func<TimeSpan, Task> _delay;

    public RetryingRelevanceScorer(
        IRelevanceScorer inner,
        Action<string>? onRetry = null,
        Func<TimeSpan, Task>? delay = null)
    {
        _inner = inner;
        _onRetry = onRetry;
        _delay = delay ?? (wait => Task.Delay(wait));
    }

    public async Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates)
    {
        var wait = FirstWait;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await _inner.ScoreAsync(situation, candidates);
            }
            catch (HttpRequestException failure) when (attempt < MaxAttempts && IsTemporary(failure.StatusCode))
            {
                var what = failure.StatusCode is { } status ? $"{(int)status}" : "network error";
                _onRetry?.Invoke($"Jev {what}, retrying in {wait.TotalSeconds:0} s (attempt {attempt + 1} of {MaxAttempts})");
                await _delay(wait);
                wait *= 2;
            }
        }
    }

    private static bool IsTemporary(HttpStatusCode? status) =>
        status is null || status == HttpStatusCode.TooManyRequests || (int)status >= 500;
}
