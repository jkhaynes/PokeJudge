namespace PokeJudge.Clarification;

using System.Text;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// Pure string-building functions kept separate from the LLM call itself so
// the exact context sent to the model is easy to trace and test, per the
// "user is a data scientist / keep AI behavior inspectable" priority.
public static class PromptBuilder
{
    public static string BuildSufficiencyPrompt(
        string scenarioDescription, IReadOnlyList<ScoredChunk> retrievedChunks, GameState state)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Scenario:");
        sb.AppendLine(scenarioDescription);
        sb.AppendLine();

        sb.AppendLine("Retrieved passages (reason only over these):");
        AppendRetrievedChunksOrNone(sb, retrievedChunks);
        sb.AppendLine();

        sb.AppendLine("Confirmed facts so far:");
        AppendBulletedListOrNone(sb, state.ConfirmedFacts);
        sb.AppendLine();

        sb.AppendLine("Hypotheses / possible interpretations (unconfirmed -- do not rely on these to establish sufficiency):");
        AppendBulletedListOrNone(sb, state.Hypotheses);
        sb.AppendLine();

        sb.AppendLine("Given only the confirmed facts above and the retrieved passages, is this scenario sufficient to produce a ruling?");

        return sb.ToString();
    }

    public static string BuildFactExtractionPrompt(ClarifyingQuestion question, string judgeAnswer)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Clarifying question asked: \"{question.Question}\"");
        sb.AppendLine($"Judge's answer: \"{judgeAnswer}\"");
        sb.AppendLine();
        sb.AppendLine("Classify the content of the judge's answer into confirmed facts and hypotheses, per the system instructions.");

        return sb.ToString();
    }

    public static string BuildRulingPrompt(
        string scenarioDescription, GameState state, IReadOnlyList<ScoredChunk> retrievedChunks)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Scenario:");
        sb.AppendLine(scenarioDescription);
        sb.AppendLine();

        sb.AppendLine("Confirmed facts (the only facts you may rely on):");
        AppendBulletedListOrNone(sb, state.ConfirmedFacts);
        sb.AppendLine();

        sb.AppendLine("Hypotheses / possible interpretations (not confirmed -- must never be used to support the ruling):");
        AppendBulletedListOrNone(sb, state.Hypotheses);
        sb.AppendLine();

        sb.AppendLine("Retrieved passages (ground every claim in these; do not use pretrained knowledge):");
        AppendRetrievedChunksOrNone(sb, retrievedChunks);
        sb.AppendLine();

        sb.AppendLine("Produce a structured ruling per the system instructions.");

        return sb.ToString();
    }

    private static void AppendRetrievedChunksOrNone(StringBuilder sb, IReadOnlyList<ScoredChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            sb.AppendLine("(none retrieved)");
            return;
        }

        foreach (var scored in chunks)
        {
            sb.AppendLine($"[{scored.Chunk.Chunk.ChunkId}] ({scored.Chunk.Chunk.SectionId}, score {scored.Score:F4}) {scored.Chunk.Chunk.Text}");
        }
    }

    private static void AppendBulletedListOrNone(StringBuilder sb, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            sb.AppendLine("(none yet)");
            return;
        }

        foreach (var item in items)
        {
            sb.AppendLine($"- {item}");
        }
    }
}
