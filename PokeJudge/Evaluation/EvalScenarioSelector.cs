namespace PokeJudge.Evaluation;

// Pure arg-parsing/filtering for `dotnet run -- evaluate [--from <id>] [--only <id>]
// [--repeat <n>]`. `evaluate` is developer/CI-facing tooling, not part of the
// judge-facing product -- the real console/UI flow only ever makes one real request
// at a time, paced by a human typing. The free-tier chat-completion rate limit only
// bites this harness's deliberate back-to-back scenario runs, so a lightweight,
// manual resume flag is the appropriately-scoped fix -- not production-grade retry
// logic in the shared LLM client, which no judge-facing path needs. --repeat
// (Milestone 8.5) exists for the same reason --from/--only does: making the
// harness's own run-to-run behavior observable is developer tooling, not a product
// requirement.
public static class EvalScenarioSelector
{
    public static (IReadOnlyList<EvalScenario>? Scenarios, int RepeatCount, string? Error) Select(
        IReadOnlyList<string> args, IReadOnlyList<EvalScenario> allScenarios)
    {
        string? fromId = null;
        string? onlyId = null;
        var repeatCount = 1;

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--from" && args[i] != "--only" && args[i] != "--repeat")
            {
                return (null, 0,
                    $"Unrecognized argument \"{args[i]}\". Usage: evaluate [--from <scenario-id>] [--only <scenario-id>] [--repeat <n>]");
            }

            if (i + 1 >= args.Count)
            {
                return (null, 0, $"\"{args[i]}\" requires a value.");
            }

            if (args[i] == "--from")
            {
                fromId = args[++i];
            }
            else if (args[i] == "--only")
            {
                onlyId = args[++i];
            }
            else
            {
                var repeatArg = args[++i];
                if (!int.TryParse(repeatArg, out repeatCount) || repeatCount < 1)
                {
                    return (null, 0, $"\"--repeat\" requires a positive integer, got \"{repeatArg}\".");
                }
            }
        }

        if (fromId is not null && onlyId is not null)
        {
            return (null, 0, "Use either --from or --only, not both.");
        }

        if (onlyId is not null)
        {
            var match = FindById(allScenarios, onlyId);
            return match is null
                ? (null, 0, UnknownIdError(onlyId, allScenarios))
                : (new List<EvalScenario> { match }, repeatCount, null);
        }

        if (fromId is not null)
        {
            var startIndex = IndexOfId(allScenarios, fromId);
            return startIndex < 0
                ? (null, 0, UnknownIdError(fromId, allScenarios))
                : (allScenarios.Skip(startIndex).ToList(), repeatCount, null);
        }

        return (allScenarios, repeatCount, null);
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
