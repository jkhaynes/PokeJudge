using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using PokeJudge.AI;
using PokeJudge.Clarification;

// ---------------------------------------------------------------------------
// Milestone 2 — Judge-Focused Prompting, Clarification, Structured Responses
//
// Grows Milestone 1's single raw LLM call into a multi-turn clarification
// loop: assess sufficiency against a hand-authored mock corpus + known
// facts (structured output, not parsed free text), ask clarifying questions
// when insufficient, classify the judge's free-text answers into confirmed
// facts vs. hypotheses, and re-assess until sufficient or a turn cap is hit.
// See .project-plans for the plan this implements.
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
var loop = new ClarificationLoop(llmClient);

Console.WriteLine("=== PokeJudge AI — Milestone 2 Clarification Loop ===\n");
Console.WriteLine("Select a scenario:");
for (var i = 0; i < MockCorpus.Scenarios.Count; i++)
{
    Console.WriteLine($"  {i + 1}. {MockCorpus.Scenarios[i].Title}");
}

Console.Write("\nEnter scenario number: ");
var selectionInput = Console.ReadLine();
if (!int.TryParse(selectionInput, out var selection) || selection < 1 || selection > MockCorpus.Scenarios.Count)
{
    Console.Error.WriteLine("Invalid selection.");
    return 1;
}

var scenario = MockCorpus.Scenarios[selection - 1];

Console.WriteLine($"\n--- Scenario: {scenario.Title} ---\n{scenario.Description}\n");

var outcome = await loop.RunAsync(
    scenario,
    askJudge: question =>
    {
        Console.WriteLine($"\n[Clarifying question — re: {question.RelatedSnippetId}] {question.Question}");
        Console.Write("Your answer: ");
        return Task.FromResult(Console.ReadLine() ?? string.Empty);
    },
    onAssessment: result =>
    {
        Console.WriteLine(result.IsSufficient
            ? "\n[Assessment] Sufficient — drafting ruling..."
            : $"\n[Assessment] Insufficient — {result.Questions.Count} clarifying question(s) needed.");
    });

Console.WriteLine("\n=== Result ===");
Console.WriteLine($"Turns used: {outcome.TurnsUsed}");

Console.WriteLine("\nConfirmed facts:");
if (outcome.State.ConfirmedFacts.Count == 0)
{
    Console.WriteLine("  (none)");
}
foreach (var fact in outcome.State.ConfirmedFacts)
{
    Console.WriteLine($"  - {fact}");
}

Console.WriteLine("\nHypotheses (unconfirmed):");
if (outcome.State.Hypotheses.Count == 0)
{
    Console.WriteLine("  (none)");
}
foreach (var hypothesis in outcome.State.Hypotheses)
{
    Console.WriteLine($"  - {hypothesis}");
}

if (outcome.Sufficient)
{
    Console.WriteLine($"\nDraft ruling: {outcome.Draft!.RecommendedAction}");
    Console.WriteLine($"Supporting snippet IDs: {string.Join(", ", outcome.Draft.SupportingSnippetIds)}");
}
else
{
    Console.WriteLine("\nTurn cap reached without a sufficient ruling.");
}

return 0;

// ---------------------------------------------------------------------------
// Milestone 1 artifact, intentionally preserved: naive substring/regex
// checks against free text, locked in by PokeJudge.Tests/NaiveSufficiencyParserTests.cs
// as evidence for why Milestone 2 introduces schema-constrained structured
// output instead. Not called from the flow above — do not "improve" it.
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
