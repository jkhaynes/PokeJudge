namespace PokeJudge.Retrieval;

public sealed record SearchArgs(string Query, int TopK);

// Pure arg parsing for `dotnet run -- search [--top <n>] [--rerank jev] <query text>`,
// in the same error-string style as EvalScenarioSelector. `--rerank` is removed by
// RerankOption.Extract before this runs. Every word that isn't a flag or its value
// is part of the query, matching the old `string.Join(" ", args.Skip(1))`.
public static class SearchArgsParser
{
    public const int DefaultTopK = 5;

    public const string Usage = "Usage: dotnet run -- search [--top <n>] [--rerank jev] <query text>";

    public static (SearchArgs? Args, string? Error) Parse(IReadOnlyList<string> args)
    {
        var topK = DefaultTopK;
        var queryWords = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--top")
            {
                queryWords.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Count)
            {
                return (null, "\"--top\" requires a value.");
            }

            var value = args[++i];
            if (!int.TryParse(value, out topK) || topK < 1)
            {
                return (null, $"\"--top\" requires a positive integer, got \"{value}\".");
            }
        }

        return queryWords.Count == 0
            ? (null, Usage)
            : (new SearchArgs(string.Join(" ", queryWords), topK), null);
    }
}
