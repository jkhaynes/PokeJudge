using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

// ---------------------------------------------------------------------------
// Milestone 1 — First LLM Interaction
//
// The smallest possible working slice: read a scenario, send it to an LLM
// through a minimal ILlmClient abstraction, print the raw response, then
// deliberately try (and fail) to extract a reliable yes/no signal from that
// free text using naive string handling. See .project-plans for the plan
// this implements.
// ---------------------------------------------------------------------------

var config = new ConfigurationBuilder()
    .AddUserSecrets<LlmClientMarker>()
    .Build();

var apiKey = config["Gemini:ApiKey"]?.Trim();
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine(
        "Missing Gemini API key.");
    return 1;
}

var modelId = config["Gemini:Model"] ?? "gemini-flash-lite-latest";

ILlmClient llmClient = new GeminiLlmClient(apiKey, modelId);

// A handful of hand-written judge scenarios to run the raw call against.
var scenarios = new[]
{
    "During a League Challenge, a player realizes on their next turn that they forgot to take a Prize card after knocking out their opponent's Pokemon two turns ago. What should the judge do?",
    "A player used an Ability that says it can only be used once per turn, but the judge suspects it may have been used twice in the same turn. The player disagrees. How should this be resolved?",
    "A player's opponent points out, mid-game, that the player's deck has 61 cards instead of 60. What should the judge do?",
};

Console.WriteLine("=== Part 1: Raw LLM responses ===\n");

foreach (var scenario in scenarios)
{
    Console.WriteLine($"--- Scenario ---\n{scenario}\n");
    var response = await llmClient.CompleteAsync(scenario);
    Console.WriteLine($"--- Raw response ---\n{response}\n");
}

// ---------------------------------------------------------------------------
// Naive extraction exercise
//
// Goal: from the model's free-text response, extract a single yes/no signal:
// "is this scenario clear enough to rule on?" using only naive string
// handling (Contains/regex) — no structured output. Run it repeatedly across
// the same scenario and observe whether the naive parser stays consistent.
// This is expected to be unreliable; that unreliability is the point, and is
// the evidence trail for Milestone 2's structured-output requirement.
// ---------------------------------------------------------------------------

Console.WriteLine("=== Part 2: Naive sufficiency-signal extraction ===\n");

const string sufficiencyPrompt =
    "A judge is asking about the following Pokemon TCG tournament scenario:\n\n" +
    "\"{0}\"\n\n" +
    "Is the information given sufficient to make a ruling, or are more details needed? " +
    "Answer in a sentence or two.";

const int repeatsPerScenario = 4;

foreach (var scenario in scenarios)
{
    Console.WriteLine($"--- Scenario ---\n{scenario}\n");

    for (var i = 1; i <= repeatsPerScenario; i++)
    {
        var prompt = string.Format(sufficiencyPrompt, scenario);
        var response = await llmClient.CompleteAsync(prompt);
        var naiveVerdict = NaiveSufficiencyParser.Parse(response);

        Console.WriteLine($"[Run {i}] Naive parser verdict: {naiveVerdict}");
        Console.WriteLine($"[Run {i}] Raw response: {response}\n");
    }
}

Console.WriteLine(
    "Observe the runs above: does the naive parser's verdict stay consistent " +
    "for semantically similar answers, or does phrasing variation cause it to " +
    "misclassify sufficiency?");

return 0;

// ---------------------------------------------------------------------------
// Minimal provider abstraction. One method, one implementation. Deliberately
// not adding retry policies, streaming, or configuration layers here — those
// aren't needed yet.
// ---------------------------------------------------------------------------
interface ILlmClient
{
    Task<string> CompleteAsync(string prompt);
}

sealed class GeminiLlmClient : ILlmClient
{
    private static readonly HttpClient Http = new();

    private readonly string _apiKey;
    private readonly string _modelId;

    public GeminiLlmClient(string apiKey, string modelId)
    {
        _apiKey = apiKey;
        _modelId = modelId;
    }

    public async Task<string> CompleteAsync(string prompt)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelId}:generateContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            })
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

        return candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }
}

// Marker type purely to anchor AddUserSecrets<T> to this assembly.
sealed class LlmClientMarker;

// ---------------------------------------------------------------------------
// Deliberately naive: substring/regex checks against free text. This is the
// thing Milestone 1 is supposed to demonstrate as unreliable — do not
// "improve" it into something more robust. That's Milestone 2's job.
// ---------------------------------------------------------------------------
static class NaiveSufficiencyParser
{
    private static readonly Regex InsufficientPattern = new(
        @"\b(not enough|insufficient|more (details|information|info)|need(s)? (more|additional)|unclear|cannot determine|can't determine|further (details|information))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SufficientPattern = new(
        @"\b(sufficient|enough information|enough details|clear enough|yes\b)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Parse(string rawResponse)
    {
        var isInsufficient = InsufficientPattern.IsMatch(rawResponse);
        var isSufficient = SufficientPattern.IsMatch(rawResponse);

        if (isInsufficient && isSufficient)
        {
            return "AMBIGUOUS (both patterns matched)";
        }

        if (isInsufficient)
        {
            return "INSUFFICIENT";
        }

        if (isSufficient)
        {
            return "SUFFICIENT";
        }

        return "UNKNOWN (neither pattern matched)";
    }
}
