namespace PokeJudge.AI;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using PokeJudge.Retrieval;

// Scores retrieved passages with TypeSafe's Jev, a classification model: one request
// whose state holds the situation and every candidate passage, with one `noul`
// (probability-of-yes) question per passage, answered in a single parallel pass.
// Endpoint and body shape are from TypeSafe's published guides; the response shape is
// isolated in JevResponseParser and confirmed by the plan's Task 10 probe. Same
// fail-loudly pattern as GeminiEmbeddingClient.
public sealed class JevRelevanceScorer : IRelevanceScorer
{
    private static readonly HttpClient Http = new();

    internal const string Endpoint = "https://api.typesafe.ai/v1/systemone";

    // {0} is the passage id. Asks whether the passage is about this situation, not
    // topical overlap -- the distinction cosine similarity can't make (deck-under-60's
    // failure). Tuning v4 (2026-10-08): the first wording asked only for "the outcome or
    // the correct remedy", so a passage that names the infraction without a remedy
    // scored low. Asking only about broken rules (v3) made plain rules questions favour
    // penalty excerpts, so v4 names both. See
    // docs/superpowers/plans/2026-10-07-jev-tuning-tracker.html.
    internal const string Instructions =
        "Does passage {0} specifically address this situation or question: the game or tournament rule that answers " +
        "it, the infraction it counts as, or how a judge should correct or penalize it? Answer yes for a passage about " +
        "this specific situation or rule, not for passages that only share words with it.";

    private readonly string _apiKey;
    private readonly string _modelId;

    public JevRelevanceScorer(string apiKey, string modelId)
    {
        _apiKey = apiKey;
        _modelId = modelId;
    }

    public async Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<double>();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(BuildRequestBody(_modelId, situation, candidates))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var httpResponse = await Http.SendAsync(request);
        if (!httpResponse.IsSuccessStatusCode)
        {
            throw RequestFailed(httpResponse.StatusCode, await httpResponse.Content.ReadAsStringAsync());
        }

        var responseBody = await httpResponse.Content.ReadAsStringAsync();

        return JevResponseParser.Parse(responseBody, QuestionKeys(candidates.Count));
    }

    // Carries the status code so RetryingRelevanceScorer can tell a 503 from a bad key.
    internal static HttpRequestException RequestFailed(System.Net.HttpStatusCode status, string body) =>
        new($"Jev API request failed ({(int)status} {status}): {body}", null, status);

    internal static IReadOnlyList<string> QuestionKeys(int count) =>
        Enumerable.Range(0, count).Select(i => $"p{i}").ToList();

    internal static object BuildRequestBody(string modelId, string situation, IReadOnlyList<ScoredChunk> candidates)
    {
        var keys = QuestionKeys(candidates.Count);

        return new
        {
            model = modelId,
            state = new
            {
                situation,
                passages = candidates.Select((c, i) => new
                {
                    id = keys[i],
                    source = c.Chunk.Chunk.ChunkId,
                    text = c.Chunk.Chunk.Text
                }).ToList()
            },
            questions = keys.ToDictionary(
                key => key,
                key => (object)new { type = "noul", instructions = string.Format(Instructions, key) })
        };
    }
}
