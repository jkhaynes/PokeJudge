namespace PokeJudge.AI;

// Reads the Jev user-secrets only when a command asks for `--rerank jev`, so runs
// without reranking never need a Jev key. Never echoes the key itself.
public sealed record JevSettings(string ApiKey, string Model, int CandidateCount)
{
    public const string DefaultModel = "jev-latest";
    public const int DefaultCandidateCount = 30;

    public static (JevSettings? Settings, string? Error) Read(Func<string, string?> get)
    {
        var apiKey = get("Jev:ApiKey")?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (null, "Missing Jev API key. Set it with: dotnet user-secrets set \"Jev:ApiKey\" \"<your-key>\" --project PokeJudge");
        }

        var candidateCount = DefaultCandidateCount;
        var candidateSetting = get("Jev:CandidateCount");
        if (!string.IsNullOrWhiteSpace(candidateSetting)
            && (!int.TryParse(candidateSetting, out candidateCount) || candidateCount < 1))
        {
            return (null, $"Jev:CandidateCount must be a positive integer, got \"{candidateSetting}\".");
        }

        var model = get("Jev:Model");
        return (new JevSettings(apiKey, string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim(), candidateCount), null);
    }
}
