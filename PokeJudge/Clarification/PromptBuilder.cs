namespace PokeJudge.Clarification;

using System.Text;
using PokeJudge.StructuredState;

// Pure string-building functions kept separate from the LLM call itself so
// the exact context sent to the model is easy to trace and test, per the
// "user is a data scientist / keep AI behavior inspectable" priority.
public static class PromptBuilder
{
    public static string BuildSufficiencyPrompt(MockScenario scenario, GameState state)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Scenario:");
        sb.AppendLine(scenario.Description);
        sb.AppendLine();

        sb.AppendLine("Supplied policy snippets (reason only over these):");
        foreach (var snippet in scenario.Snippets)
        {
            sb.AppendLine($"[{snippet.Id}] {snippet.Text}");
        }
        sb.AppendLine();

        sb.AppendLine("Confirmed facts so far:");
        AppendBulletedListOrNone(sb, state.ConfirmedFacts);
        sb.AppendLine();

        sb.AppendLine("Hypotheses / possible interpretations (unconfirmed -- do not rely on these to establish sufficiency):");
        AppendBulletedListOrNone(sb, state.Hypotheses);
        sb.AppendLine();

        sb.AppendLine("Given only the confirmed facts above and the supplied snippets, is this scenario sufficient to produce a draft ruling?");

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
