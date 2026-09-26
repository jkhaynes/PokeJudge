namespace PokeJudge.Evaluation;

using System.Text.Json;
using PokeJudge.AI;

public sealed record JudgeAnswer(string Answer, bool Known);

// Stands in for the human judge at the table during `evaluate`: answers each of
// PokeJudge's clarifying questions from the scenario's fact sheet, and only from it.
// Replaces the old ordered script, which handed out canned answers regardless of
// what was actually asked. Eval-only: the judge-facing flow still asks a real person.
public sealed class SimulatedJudge
{
    public const string NotKnown = "Not known.";

    private const string SystemPrompt =
        "You are a Pokémon TCG tournament judge standing at the table, answering questions " +
        "from an assistant about what happened. Answer ONLY from the fact sheet you are given. " +
        "Never invent, guess, or infer facts that are not stated in it. Answer briefly, in one " +
        "or two sentences. If the fact sheet does not answer the question, set known to false.";

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

    public async Task<JudgeAnswer> AnswerAsync(string factSheet, string question)
    {
        var userContent = $"Fact sheet:\n{factSheet}\n\nQuestion:\n{question}";
        var answer = await _llmClient.CompleteStructuredAsync<JudgeAnswer>(SystemPrompt, userContent, Schema);

        // A "not known" reply must not leak a guess into PokeJudge's facts.
        return answer.Known ? answer : answer with { Answer = NotKnown };
    }
}
