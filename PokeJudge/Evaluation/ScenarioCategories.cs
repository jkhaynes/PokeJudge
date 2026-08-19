namespace PokeJudge.Evaluation;

// The small, consistent category vocabulary every EvalScenario.Category value must
// come from -- normalized during Milestone 8.5 after the original 8 scenarios
// accumulated inconsistent, overlapping free-text values (e.g. "Illegal Game State"
// vs. "Illegal Game State / Discretion Required"). Enforced by EvalDatasetTests
// rather than promoting Category to an enum, since Category also doubles as
// free-text console display text and an enum would need a parallel display-name
// mapping for no real benefit at this dataset's size.
public static class ScenarioCategories
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>
    {
        "Tournament Procedure",
        "Deck/Decklist Issues",
        "Illegal Game State",
        "Discretion Required",
        "Penalty Questions",
        "Prize Errors",
        "Gameplay Error",
        "Attack Resolution",
        "Timing Questions",
    };
}
