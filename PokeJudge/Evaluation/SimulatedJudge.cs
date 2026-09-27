namespace PokeJudge.Evaluation;

using System.Text.Json;
using PokeJudge.AI;

public sealed record JudgeAnswer(string Answer, bool Known);

// Stands in for the human judge at the table during `evaluate`: answers each of
// PokeJudge's clarifying questions from the scenario text and its fact sheet, and only from them.
// Replaces the old ordered script, which handed out canned answers regardless of
// what was actually asked. Eval-only: the judge-facing flow still asks a real person.
public sealed class SimulatedJudge
{
    public const string NotKnown = "Not known.";

    private const string SystemPrompt =
        "You are a Pokémon TCG tournament judge standing at the table, answering questions " +
        "from an assistant about what happened. Answer ONLY from the scenario you described and " +
        "the fact sheet you are given. Never invent, guess, or infer facts that are not stated " +
        "in them. Answer briefly, in one or two sentences. If the question has several parts, " +
        "answer every part they cover and say which parts they don't. Set known to false only " +
        "if they cover none of the question.";

    private static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "OBJECT",
          "properties": {
            "answer": { "type": "STRING" },
            "known": { "type": "BOOLEAN" }
          },
          "required": ["answer", "known"]
        }
        """).RootElement;

    private readonly ILlmClient _llmClient;

    public SimulatedJudge(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    // The scenario is what this judge told PokeJudge, so its facts count as known too.
    public async Task<JudgeAnswer> AnswerAsync(string scenario, string factSheet, string question)
    {
        var userContent = $"Scenario you described:\n{scenario}\n\nFact sheet:\n{factSheet}\n\nQuestion:\n{question}";
        var answer = await _llmClient.CompleteStructuredAsync<JudgeAnswer>(SystemPrompt, userContent, Schema);

        // A "not known" reply must not leak a guess into PokeJudge's facts.
        return answer.Known ? answer : answer with { Answer = NotKnown };
    }
}
