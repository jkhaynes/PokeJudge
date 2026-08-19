namespace PokeJudge.Evaluation;

// Pure arg-parsing/filtering for `dotnet run -- evaluate [--from <id>] [--only <id>]`.
// `evaluate` is developer/CI-facing tooling, not part of the judge-facing product --
// the real console/UI flow only ever makes one real request at a time, paced by a
// human typing. The free-tier chat-completion rate limit only bites this harness's
// deliberate back-to-back scenario runs, so a lightweight, manual resume flag is the
// appropriately-scoped fix -- not production-grade retry logic in the shared LLM
// client, which no judge-facing path needs.
public static class EvalScenarioSelector
{
    public static (IReadOnlyList<EvalScenario>? Scenarios, string? Error) Select(
        IReadOnlyList<string> args, IReadOnlyList<EvalScenario> allScenarios)
    {
        string? fromId = null;
        string? onlyId = null;

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--from" && args[i] != "--only")
            {
                return (null, $"Unrecognized argument \"{args[i]}\". Usage: evaluate [--from <scenario-id>] [--only <scenario-id>]");
            }

            if (i + 1 >= args.Count)
            {
                return (null, $"\"{args[i]}\" requires a scenario id.");
            }

            if (args[i] == "--from")
            {
                fromId = args[++i];
            }
            else
            {
                onlyId = args[++i];
            }
        }

        if (fromId is not null && onlyId is not null)
        {
            return (null, "Use either --from or --only, not both.");
        }

        if (onlyId is not null)
        {
            var match = FindById(allScenarios, onlyId);
            return match is null
                ? (null, UnknownIdError(onlyId, allScenarios))
                : (new List<EvalScenario> { match }, null);
        }

        if (fromId is not null)
        {
            var startIndex = IndexOfId(allScenarios, fromId);
            return startIndex < 0
                ? (null, UnknownIdError(fromId, allScenarios))
                : (allScenarios.Skip(startIndex).ToList(), null);
        }

        return (allScenarios, null);
    }

    private static EvalScenario? FindById(IReadOnlyList<EvalScenario> scenarios, string id) =>
        scenarios.FirstOrDefault(s => s.Id == id);

    private static int IndexOfId(IReadOnlyList<EvalScenario> scenarios, string id)
    {
        for (var i = 0; i < scenarios.Count; i++)
        {
            if (scenarios[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static string UnknownIdError(string id, IReadOnlyList<EvalScenario> allScenarios) =>
        $"Unknown scenario id \"{id}\". Known ids: {string.Join(", ", allScenarios.Select(s => s.Id))}";
}
