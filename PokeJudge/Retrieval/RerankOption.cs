namespace PokeJudge.Retrieval;

// Strips the optional `--rerank <name>` flag before a command's own argument parsing
// runs, so every retrieving command shares one definition and EvalScenarioSelector
// stays unchanged. Jev reranking is on by default; `--rerank none` turns it off for a
// run (plain cosine top-K), which is how the no-reranking baseline is measured.
public static class RerankOption
{
    public const string Jev = "jev";
    public const string None = "none";

    // Rerank is the reranker to use, or null for none.
    public static (IReadOnlyList<string> Remaining, string? Rerank, string? Error) Extract(IReadOnlyList<string> args)
    {
        var remaining = new List<string>();
        string? value = null;

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

            if (value is not null)
            {
                return (remaining, null, "\"--rerank\" was given more than once.");
            }

            value = args[++i];
            if (value is not (Jev or None))
            {
                return (remaining, null, $"Unknown reranker \"{value}\". Supported: {Jev}, {None}.");
            }
        }

        return (remaining, value == None ? null : Jev, null);
    }
}
