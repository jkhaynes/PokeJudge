namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Evaluation;
using PokeJudge.StructuredState;

public class EvalScenarioSelectorTests
{
    private static EvalScenario Scenario(string id) => new(
        id, "Category", $"Description for {id}",
        Array.Empty<string>(), ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
        ScriptedAnswer: null, ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
        AcceptableFinalSourceSupport: null);

    private static readonly IReadOnlyList<EvalScenario> All = new[]
    {
        Scenario("a"), Scenario("b"), Scenario("c"), Scenario("d"),
    };

    [Fact]
    public void Select_NoArgs_ReturnsAllScenarios()
    {
        var (scenarios, error) = EvalScenarioSelector.Select(Array.Empty<string>(), All);

        Assert.Null(error);
        Assert.Equal(All, scenarios);
    }

    [Fact]
    public void Select_FromAMiddleScenario_ReturnsThatScenarioOnward()
    {
        var (scenarios, error) = EvalScenarioSelector.Select(new[] { "--from", "c" }, All);

        Assert.Null(error);
        Assert.Equal(new[] { "c", "d" }, scenarios!.Select(s => s.Id));
    }

    [Fact]
    public void Select_FromTheFirstScenario_ReturnsAllScenarios()
    {
        var (scenarios, error) = EvalScenarioSelector.Select(new[] { "--from", "a" }, All);

        Assert.Null(error);
        Assert.Equal(new[] { "a", "b", "c", "d" }, scenarios!.Select(s => s.Id));
    }

    [Fact]
    public void Select_FromAnUnknownId_ReturnsAnErrorNamingIt()
    {
        var (scenarios, error) = EvalScenarioSelector.Select(new[] { "--from", "nope" }, All);

        Assert.Null(scenarios);
        Assert.Contains("nope", error);
    }

    [Fact]
    public void Select_OnlyAKnownId_ReturnsJustThatScenario()
    {
        var (scenarios, error) = EvalScenarioSelector.Select(new[] { "--only", "b" }, All);

        Assert.Null(error);
        Assert.Equal(new[] { "b" }, scenarios!.Select(s => s.Id));
    }

    [Fact]
    public void Select_OnlyAnUnknownId_ReturnsAnErrorNamingIt()
    {
        var (scenarios, error) = EvalScenarioSelector.Select(new[] { "--only", "nope" }, All);

        Assert.Null(scenarios);
        Assert.Contains("nope", error);
    }

    [Fact]
    public void Select_BothFromAndOnly_ReturnsAMutualExclusionError()
    {
        var (scenarios, error) = EvalScenarioSelector.Select(new[] { "--from", "a", "--only", "b" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }

    [Fact]
    public void Select_UnrecognizedFlag_ReturnsAnError()
    {
        var (scenarios, error) = EvalScenarioSelector.Select(new[] { "--bogus" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }

    [Fact]
    public void Select_FlagWithNoValue_ReturnsAnErrorRatherThanThrowing()
    {
        var (scenarios, error) = EvalScenarioSelector.Select(new[] { "--from" }, All);

        Assert.Null(scenarios);
        Assert.NotNull(error);
    }
}
