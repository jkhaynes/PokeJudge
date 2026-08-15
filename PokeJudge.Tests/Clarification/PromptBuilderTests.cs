namespace PokeJudge.Tests.Clarification;

using PokeJudge.Clarification;
using PokeJudge.StructuredState;

public class PromptBuilderTests
{
    private static readonly MockScenario Scenario = new(
        "Test Scenario",
        "A test scenario description.",
        new[]
        {
            new PolicySnippet("X1", "Material snippet text."),
            new PolicySnippet("X2", "Irrelevant snippet text."),
        });

    [Fact]
    public void BuildSufficiencyPrompt_IncludesScenarioDescriptionAndAllSnippets()
    {
        var prompt = PromptBuilder.BuildSufficiencyPrompt(Scenario, new GameState());

        Assert.Contains("A test scenario description.", prompt);
        Assert.Contains("X1", prompt);
        Assert.Contains("Material snippet text.", prompt);
        Assert.Contains("X2", prompt);
        Assert.Contains("Irrelevant snippet text.", prompt);
    }

    [Fact]
    public void BuildSufficiencyPrompt_WithNoFactsYet_StillLabelsBothSections()
    {
        var prompt = PromptBuilder.BuildSufficiencyPrompt(Scenario, new GameState());

        Assert.Contains("Confirmed facts", prompt);
        Assert.Contains("Hypotheses", prompt);
    }

    [Fact]
    public void BuildSufficiencyPrompt_IncludesConfirmedFactsAndHypotheses_WhenPresent()
    {
        var state = new GameState();
        state.AddConfirmedFacts(new[] { "A Pokemon was Knocked Out." });
        state.AddHypotheses(new[] { "The player probably lost track of time." });

        var prompt = PromptBuilder.BuildSufficiencyPrompt(Scenario, state);

        Assert.Contains("A Pokemon was Knocked Out.", prompt);
        Assert.Contains("The player probably lost track of time.", prompt);
    }

    [Fact]
    public void BuildFactExtractionPrompt_IncludesQuestionAndAnswer()
    {
        var question = new ClarifyingQuestion("Was a Pokemon Knocked Out?", "X1");

        var prompt = PromptBuilder.BuildFactExtractionPrompt(question, "Yes, it was.");

        Assert.Contains("Was a Pokemon Knocked Out?", prompt);
        Assert.Contains("Yes, it was.", prompt);
    }
}
