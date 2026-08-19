namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Evaluation;
using PokeJudge.StructuredState;

public class EvalScenarioSelectorTests
{
    private static EvalScenario Scenario(string id) => new(
        id, "Category", $"Description for {id}",
        Array.Empty<string>(), ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
        ScriptedAnswers: Array.Empty<string>(), ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
        AcceptableFinalSourceSupport: null);

    private static readonly IReadOnlyList<EvalScenario> All = new[]
    {
        Scenario("a"), Scenario("b"), Scenario("c"), Scenario("d"),
    };

    [Fact]
    public void Select_NoArgs_ReturnsAllScenariosWithDefaultRepeatCountOfOne()
    {
        var (scenarios, repeatCount, error) = EvalScenarioSelector.Select(Array.Empty<string>(), All);

        Assert.Null(error);
        Assert.Equal(All, scenarios);
        Assert.Equal(1, repeatCount);
    }

    [Fact]
    public void Select_FromAMiddleScenario_ReturnsThatScenarioOnward()
    {
        var (scenarios, repeatCount, error) = EvalScenarioSelector.Select(new[] { "--from", "c" }, All);

        Assert.Null(error);
        Assert.Equal(new[] { "c", "d" }, scenarios!.Select(s => s.Id));
        Assert.Equal(1, repeatCount);
    }

    [Fact]
    public void Select_FromTheFirstScenario_ReturnsAllScenarios()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--from", "a" }, All);

        Assert.Null(error);
        Assert.Equal(new[] { "a", "b", "c", "d" }, scenarios!.Select(s => s.Id));
    }

    [Fact]
    public void Select_FromAnUnknownId_ReturnsAnErrorNamingIt()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--from", "nope" }, All);

        Assert.Null(scenarios);
        Assert.Contains("nope", error);
    }

    [Fact]
    public void Select_OnlyAKnownId_ReturnsJustThatScenario()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--only", "b" }, All);

        Assert.Null(error);
        Assert.Equal(new[] { "b" }, scenarios!.Select(s => s.Id));
    }

    [Fact]
    public void Select_OnlyAnUnknownId_ReturnsAnErrorNamingIt()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--only", "nope" }, All);

        Assert.Null(scenarios);
        Assert.Contains("nope", error);
    }

    [Fact]
    public void Select_BothFromAndOnly_ReturnsAMutualExclusionError()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--from", "a", "--only", "b" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }

    [Fact]
    public void Select_UnrecognizedFlag_ReturnsAnError()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--bogus" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }

    [Fact]
    public void Select_FlagWithNoValue_ReturnsAnErrorRatherThanThrowing()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--from" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }

    // --- --repeat ---

    [Fact]
    public void Select_RepeatWithValidCount_ReturnsThatCount()
    {
        var (scenarios, repeatCount, error) = EvalScenarioSelector.Select(new[] { "--repeat", "3" }, All);

        Assert.Null(error);
        Assert.Equal(All, scenarios);
        Assert.Equal(3, repeatCount);
    }

    [Fact]
    public void Select_RepeatCombinedWithOnly_AppliesToTheSelectedScenario()
    {
        var (scenarios, repeatCount, error) = EvalScenarioSelector.Select(new[] { "--only", "b", "--repeat", "5" }, All);

        Assert.Null(error);
        Assert.Equal(new[] { "b" }, scenarios!.Select(s => s.Id));
        Assert.Equal(5, repeatCount);
    }

    [Fact]
    public void Select_RepeatCombinedWithFrom_AppliesToTheSelectedScenarios()
    {
        var (scenarios, repeatCount, error) = EvalScenarioSelector.Select(new[] { "--from", "c", "--repeat", "2" }, All);

        Assert.Null(error);
        Assert.Equal(new[] { "c", "d" }, scenarios!.Select(s => s.Id));
        Assert.Equal(2, repeatCount);
    }

    [Fact]
    public void Select_RepeatZero_ReturnsAnError()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--repeat", "0" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }

    [Fact]
    public void Select_RepeatNegative_ReturnsAnError()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--repeat", "-1" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }

    [Fact]
    public void Select_RepeatNonNumeric_ReturnsAnError()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--repeat", "abc" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }

    [Fact]
    public void Select_RepeatWithNoValue_ReturnsAnErrorRatherThanThrowing()
    {
        var (scenarios, _, error) = EvalScenarioSelector.Select(new[] { "--repeat" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }
}
