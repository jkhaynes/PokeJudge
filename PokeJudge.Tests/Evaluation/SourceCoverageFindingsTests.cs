namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Evaluation;

public class SourceCoverageFindingsTests
{
    [Fact]
    public void All_IsNotEmpty()
    {
        Assert.NotEmpty(SourceCoverageFindings.All);
    }

    [Fact]
    public void All_EveryFindingReferencesARealScenarioId()
    {
        var knownIds = EvalDataset.Scenarios.Select(s => s.Id).ToHashSet();

        foreach (var finding in SourceCoverageFindings.All)
        {
            Assert.Contains(finding.ScenarioId, knownIds);
        }
    }

    [Fact]
    public void All_ScenarioIdsAreUnique()
    {
        var ids = SourceCoverageFindings.All.Select(f => f.ScenarioId).ToList();

        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void All_PossibleSourceGapFindings_RecordWhatInformationIsMissing()
    {
        foreach (var finding in SourceCoverageFindings.All.Where(
            f => f.Level is SourceCoverageLevel.PossibleSourceGap or SourceCoverageLevel.ConfirmedSourceGap))
        {
            Assert.False(string.IsNullOrWhiteSpace(finding.MissingInformation));
        }
    }
}
