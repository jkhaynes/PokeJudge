namespace PokeJudge.AI;

using System.Net.Http.Json;
using System.Text.Json;

// Extends Milestone 1's raw generateContent call with two things Gemini
// supports natively: a system_instruction field (a distinct channel from
// the user content) and generationConfig.responseSchema (schema-constrained
// JSON output), so the model's response can be deserialized directly
// instead of parsed as free text.
public sealed class GeminiLlmClient : ILlmClient
{
    private static readonly HttpClient Http = new();

    // Without a seed Gemini picks a random one per request, so identical inputs can
    // produce different rulings. Temperature stays at the default 1.0: Google warns that
    // lowering it on Gemini 3 models can cause looping or degraded reasoning.
    internal const int Seed = 42;

    private readonly string _apiKey;
    private readonly string _modelId;

    public GeminiLlmClient(string apiKey, string modelId)
    {
        _apiKey = apiKey;
        _modelId = modelId;
    }

    public async Task<T> CompleteStructuredAsync<T>(string systemInstruction, string userContent, JsonElement responseSchema)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelId}:generateContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(BuildRequestBody(systemInstruction, userContent, responseSchema))
        };
        request.Headers.Add("x-goog-api-key", _apiKey);

        var httpResponse = await Http.SendAsync(request);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorBody = await httpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Gemini API request failed ({(int)httpResponse.StatusCode} {httpResponse.StatusCode}): {errorBody}");
        }

        using var responseStream = await httpResponse.Content.ReadAsStreamAsync();
        using var responseJson = await JsonDocument.ParseAsync(responseStream);

        if (!responseJson.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            throw new HttpRequestException(
                $"Gemini API returned no candidates (likely blocked or filtered): {responseJson.RootElement}");
        }

        var rawJsonText = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        return StructuredResponseParser.Parse<T>(rawJsonText);
    }

    internal static object BuildRequestBody(string systemInstruction, string userContent, JsonElement responseSchema) => new
    {
        system_instruction = new { parts = new[] { new { text = systemInstruction } } },
        contents = new[]
        {
            new { role = "user", parts = new[] { new { text = userContent } } }
        },
        generationConfig = new
        {
            responseMimeType = "application/json",
            responseSchema,
            seed = Seed
        }
    };
}
