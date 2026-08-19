namespace PokeJudge.Evaluation;

public sealed record CategoryOutcome(string Category, int Passed, int Total);

// Pure aggregation over already-scored results -- no LLM, no I/O. Groups by
// Category in first-seen order (the order scenarios actually ran in) rather than
// alphabetizing, so the printed summary reads in the same order as the run it
// summarizes.
public static class CategorySummary
{
    public static IReadOnlyList<CategoryOutcome> Summarize(IEnumerable<(string Category, bool Passed)> results)
    {
        var order = new List<string>();
        var counts = new Dictionary<string, (int Passed, int Total)>();

        foreach (var (category, passed) in results)
        {
            if (!counts.TryGetValue(category, out var current))
            {
                order.Add(category);
                current = (0, 0);
            }

            counts[category] = (passed ? current.Passed + 1 : current.Passed, current.Total + 1);
        }

        return order.Select(c => new CategoryOutcome(c, counts[c].Passed, counts[c].Total)).ToList();
    }
}
