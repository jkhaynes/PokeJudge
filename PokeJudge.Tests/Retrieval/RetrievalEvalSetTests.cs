namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Retrieval;

public class RetrievalEvalSetTests
{
    [Fact]
    public void HasBetweenSixAndEightCases()
    {
        Assert.InRange(RetrievalEvalSet.Cases.Count, 6, 8);
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
}
