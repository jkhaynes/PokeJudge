namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Evaluation;
using PokeJudge.Retrieval;

public class RetrievalEvalSetTests
{
    [Fact]
    public void HasBetweenTwentyFiveAndThirtyFiveCases()
    {
        Assert.InRange(RetrievalEvalSet.Cases.Count, 25, 35);
    }

    [Fact]
    public void EveryCase_HasNonEmptyQueryAndExpectedSectionId()
    {
        Assert.All(RetrievalEvalSet.Cases, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Query));
            Assert.False(string.IsNullOrWhiteSpace(c.ExpectedSectionId));
        });
    }

    [Fact]
    public void CoversBothRealDocuments()
    {
        Assert.Contains(RetrievalEvalSet.Cases, c => c.ExpectedSectionId.StartsWith("PPTRH-"));
        Assert.Contains(RetrievalEvalSet.Cases, c => c.ExpectedSectionId.StartsWith("TCGTH-"));
    }

    // Step 4: the search-only test must reach every PPG and TCGRULES section a scenario
    // turns on, so a retrieval change is measured where the scenarios need it.
    [Fact]
    public void CoversEveryPpgAndTcgRulesSectionTheScenariosExpect()
    {
        var expected = EvalDataset.Scenarios
            .SelectMany(s => s.ExpectedMaterialSectionIds.Concat(s.ExpectedMaterialSectionIdsAfterAnswer))
            .Where(id => id.StartsWith("PPG-") || id.StartsWith("TCGRULES-"))
            .Distinct();
        var covered = RetrievalEvalSet.Cases.Select(c => c.ExpectedSectionId).ToHashSet();

        Assert.Empty(expected.Where(id => !covered.Contains(id)));
    }
}
