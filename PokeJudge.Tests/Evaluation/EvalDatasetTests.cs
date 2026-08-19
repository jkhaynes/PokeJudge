namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Evaluation;

// Deterministic invariants over the hand-authored dataset itself -- not testing model
// behavior, just the static data every real evaluate run reads. Milestone 8.5 grew
// this dataset and normalized its category vocabulary; these checks make both
// requirements structurally enforced instead of just a one-time manual pass.
public class EvalDatasetTests
{
    [Fact]
    public void Scenarios_CountFallsWithinTheMilestone85HardeningTarget()
    {
        Assert.InRange(EvalDataset.Scenarios.Count, 20, 30);
    }

    [Fact]
    public void Scenarios_IdsAreUnique()
    {
        var ids = EvalDataset.Scenarios.Select(s => s.Id).ToList();

        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void Scenarios_EveryCategoryIsInTheAllowedSet()
    {
        foreach (var scenario in EvalDataset.Scenarios)
        {
            Assert.Contains(scenario.Category, ScenarioCategories.Allowed);
        }
    }

    [Fact]
    public void Scenarios_EveryRequiredCategoryHasAtLeastOneScenario()
    {
        var usedCategories = EvalDataset.Scenarios.Select(s => s.Category).ToHashSet();

        foreach (var category in ScenarioCategories.Allowed)
        {
            Assert.Contains(category, usedCategories);
        }
    }

    [Fact]
    public void Scenarios_RequiresOneClarification_AlwaysHasAtLeastOneScriptedAnswer()
    {
        foreach (var scenario in EvalDataset.Scenarios.Where(
            s => s.ExpectedOutcome == ExpectedTrajectoryOutcome.RequiresOneClarification))
        {
            Assert.NotEmpty(scenario.ScriptedAnswers);
            Assert.All(scenario.ScriptedAnswers, answer => Assert.False(string.IsNullOrWhiteSpace(answer)));
        }
    }
}
