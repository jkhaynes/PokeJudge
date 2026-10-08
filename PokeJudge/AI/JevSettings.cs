namespace PokeJudge.AI;

// Reads the Jev user-secrets only when a command asks for `--rerank jev`, so runs
// without reranking never need a Jev key. Never echoes the key itself.
public sealed record JevSettings(string ApiKey, string Model, int CandidateCount, int MaxPerSection = JevSettings.DefaultMaxPerSection)
{
    public const string DefaultModel = "jev-latest";
    public const int DefaultCandidateCount = 30;

    // At most 4 of the 5 reranked excerpts from one section, so at least one slot goes
    // to another section. Tuning v5: ace-spec-count needs 4 excerpts of PPG-5.6.1, and
    // supporter-twice needs a 5th slot free of PPG-4.2.1.
    public const int DefaultMaxPerSection = 4;

    public static (JevSettings? Settings, string? Error) Read(Func<string, string?> get)
    {
        var apiKey = get("Jev:ApiKey")?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (null, "Missing Jev API key. Set it with: dotnet user-secrets set \"Jev:ApiKey\" \"<your-key>\" --project PokeJudge");
        }

        var (candidateCount, candidateError) = ReadPositive(get, "Jev:CandidateCount", DefaultCandidateCount);
        if (candidateError is not null)
        {
            return (null, candidateError);
        }

        var (maxPerSection, maxError) = ReadPositive(get, "Jev:MaxPerSection", DefaultMaxPerSection);
        if (maxError is not null)
        {
            return (null, maxError);
        }

        var model = get("Jev:Model");
        return (new JevSettings(apiKey, string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim(), candidateCount, maxPerSection), null);
    }

    private static (int Value, string? Error) ReadPositive(Func<string, string?> get, string key, int defaultValue)
    {
        var setting = get(key);
        if (string.IsNullOrWhiteSpace(setting))
        {
            return (defaultValue, null);
        }

        return int.TryParse(setting, out var value) && value >= 1
            ? (value, null)
            : (0, $"{key} must be a positive integer, got \"{setting}\".");
    }
}
