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

    private static EvalScenario ById(string id) => EvalDataset.Scenarios.Single(s => s.Id == id);

    [Fact]
    public void DeckNotShuffled_ExpectsAQuestionAboutWhenItWasNoticed()
    {
        var scenario = ById("deck-not-shuffled");

        Assert.Equal(ExpectedTrajectoryOutcome.RequiresOneClarification, scenario.ExpectedOutcome);
        Assert.Contains("TCGTH-6.2.2", scenario.ExpectedMaterialSectionIds);
        Assert.Empty(scenario.ExpectedMaterialSectionIdsAfterAnswer);
    }

    [Fact]
    public void SpectatorBadges_NamesTheEventLevelTheRuleUses()
    {
        Assert.Contains("Regional Championship", ById("spectator-badges").InitialDescription);
    }

    [Fact]
    public void MissedMulliganDraws_ReplacesTheUnanswerableMulliganScenario()
    {
        Assert.DoesNotContain(EvalDataset.Scenarios, s => s.Id == "mulligan-not-taken");

        var scenario = ById("missed-mulligan-draws");

        Assert.Equal("Timing Questions", scenario.Category);
        Assert.Equal(ExpectedTrajectoryOutcome.SufficientOnFirstTurn, scenario.ExpectedOutcome);
        Assert.Contains("TCGTH-7.4.1", scenario.ExpectedMaterialSectionIds);
        Assert.Contains("TCGRULES-full-details-of-taking-a-mulligan", scenario.ExpectedMaterialSectionIds);
    }

    [Fact]
    public void AceSpecCount_StatesWhenItWasFoundAndThatTheDeckWasAlreadyPlayed()
    {
        var scenario = ById("ace-spec-count");

        Assert.Contains("round 3", scenario.InitialDescription);
        Assert.Contains("rounds 1 and 2", scenario.InitialDescription);
        Assert.Equal(ExpectedTrajectoryOutcome.SufficientOnFirstTurn, scenario.ExpectedOutcome);
        Assert.Contains("PPG-5.6.1", scenario.ExpectedMaterialSectionIds);
    }

    [Fact]
    public void Scenarios_EveryScenarioHasAFactSheet()
    {
        Assert.All(EvalDataset.Scenarios, s => Assert.False(string.IsNullOrWhiteSpace(s.FactSheet), s.Id));
    }
}
