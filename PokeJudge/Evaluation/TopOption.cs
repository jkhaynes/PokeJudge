namespace PokeJudge.Evaluation;

using PokeJudge.Clarification;

// Strips `evaluate`'s optional `--top <n>`: how many reranked excerpts the AI reads per
// turn (ClarificationLoop's topK). Default 5. Exists to compare the same version at 5
// and 10 excerpts; the interactive flow keeps the default. Same shape as RerankOption,
// so EvalScenarioSelector stays unchanged.
public static class TopOption
{
    public static (IReadOnlyList<string> Remaining, int TopK, string? Error) Extract(IReadOnlyList<string> args)
    {
        var remaining = new List<string>();
        int? topK = null;

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--top")
            {
                remaining.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Count)
            {
                return (remaining, 0, "\"--top\" requires a value.");
            }

            if (topK is not null)
            {
                return (remaining, 0, "\"--top\" was given more than once.");
            }

            var value = args[++i];
            if (!int.TryParse(value, out var parsed) || parsed < 1)
            {
                return (remaining, 0, $"\"--top\" requires a positive integer, got \"{value}\".");
            }

            topK = parsed;
        }

        return (remaining, topK ?? ClarificationLoop.DefaultTopK, null);
    }
}
