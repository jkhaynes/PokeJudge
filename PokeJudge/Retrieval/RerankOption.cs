namespace PokeJudge.Retrieval;

// Strips the optional `--rerank <name>` flag before a command's own argument parsing
// runs, so search, eval and evaluate share one definition and EvalScenarioSelector
// stays unchanged. Reranking is opt-in: no flag means today's plain cosine top-K.
public static class RerankOption
{
    public const string Jev = "jev";

    public static (IReadOnlyList<string> Remaining, string? Rerank, string? Error) Extract(IReadOnlyList<string> args)
    {
        var remaining = new List<string>();
        string? rerank = null;

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--rerank")
            {
                remaining.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Count)
            {
                return (remaining, null, "\"--rerank\" requires a value.");
            }

            if (rerank is not null)
            {
                return (remaining, null, "\"--rerank\" was given more than once.");
            }

            var value = args[++i];
            if (value != Jev)
            {
                return (remaining, null, $"Unknown reranker \"{value}\". Supported: {Jev}.");
            }

            rerank = value;
        }

        return (remaining, rerank, null);
    }
}
