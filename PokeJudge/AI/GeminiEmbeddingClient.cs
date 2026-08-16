namespace PokeJudge.AI;

using System.Net.Http.Json;

// Uses Gemini's batchEmbedContents endpoint (confirmed against the real API at
// implementation time), not one embedContent call per text -- see IEmbeddingClient.
// outputDimensionality lets the model's native (much larger) vector size be
// reduced for more compact local storage; gemini-embedding-001 supports this
// directly (a "Matryoshka" embedding model), confirmed working against the real API.
//
// Response order is assumed to match request order (GeminiEmbeddingResponseParser
// returns embeddings[i] for requests[i], with no per-item ID from the API to
// correlate by). This was verified, not just assumed: 4 distinct texts sent in one
// batch call each exactly matched an independently-obtained single-call reference
// vector at the same position, with no cross-position matches.
public sealed class GeminiEmbeddingClient : IEmbeddingClient
{
    private static readonly HttpClient Http = new();

    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly int _outputDimensionality;

    public GeminiEmbeddingClient(string apiKey, string modelId, int outputDimensionality)
    {
        _apiKey = apiKey;
        _modelId = modelId;
        _outputDimensionality = outputDimensionality;
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts)
    {
        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelId}:batchEmbedContents";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                requests = texts.Select(text => new
                {
                    model = $"models/{_modelId}",
                    content = new { parts = new[] { new { text } } },
                    outputDimensionality = _outputDimensionality
                })
            })
        };
        request.Headers.Add("x-goog-api-key", _apiKey);

        var httpResponse = await Http.SendAsync(request);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorBody = await httpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Gemini embedding API request failed ({(int)httpResponse.StatusCode} {httpResponse.StatusCode}): {errorBody}");
        }

        var responseBody = await httpResponse.Content.ReadAsStringAsync();

        return GeminiEmbeddingResponseParser.Parse(responseBody, texts.Count);
    }
}
