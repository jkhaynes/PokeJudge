namespace PokeJudge.Retrieval;

// Pure, testable: the text embedded for each retrieval call. Deliberately grows
// as facts are confirmed (PRD SS8 "iterative retrieval") -- a newly confirmed fact
// can surface passages the original, incomplete scenario description couldn't.
public static class RetrievalQueryBuilder
{
    public static string Build(string scenarioDescription, IReadOnlyList<string> confirmedFacts)
    {
        if (confirmedFacts.Count == 0)
        {
            return scenarioDescription;
        }

        return scenarioDescription + " " + string.Join(" ", confirmedFacts);
    }
}
