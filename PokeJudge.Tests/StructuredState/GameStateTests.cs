namespace PokeJudge.Tests.StructuredState;

using PokeJudge.StructuredState;

public class GameStateTests
{
    [Fact]
    public void NewGameState_HasNoFactsOrHypotheses()
    {
        var state = new GameState();

        Assert.Empty(state.ConfirmedFacts);
        Assert.Empty(state.Hypotheses);
    }

    [Fact]
    public void AddConfirmedFacts_AddsToConfirmedFactsOnly()
    {
        var state = new GameState();

        state.AddConfirmedFacts(new[] { "A Pokemon was Knocked Out." });

        Assert.Equal(new[] { "A Pokemon was Knocked Out." }, state.ConfirmedFacts);
        Assert.Empty(state.Hypotheses);
    }

    [Fact]
    public void AddHypotheses_AddsToHypothesesOnly_NeverConfirmedFacts()
    {
        var state = new GameState();

        state.AddHypotheses(new[] { "The player probably lost track of time." });

        Assert.Empty(state.ConfirmedFacts);
        Assert.Equal(new[] { "The player probably lost track of time." }, state.Hypotheses);
    }

    [Fact]
    public void AddingAcrossMultipleTurns_AccumulatesInOrder()
    {
        var state = new GameState();

        state.AddConfirmedFacts(new[] { "fact 1" });
        state.AddHypotheses(new[] { "hypothesis 1" });
        state.AddConfirmedFacts(new[] { "fact 2" });

        Assert.Equal(new[] { "fact 1", "fact 2" }, state.ConfirmedFacts);
        Assert.Equal(new[] { "hypothesis 1" }, state.Hypotheses);
    }

    [Fact]
    public void AddAskedQuestion_RecordsQuestionsAndAnswersInOrder()
    {
        var state = new GameState();

        state.AddAskedQuestion("Was a Pokemon Knocked Out?", "Yes.");
        state.AddAskedQuestion("Did they see the card?", "Not known.");

        Assert.Equal(
            new[] { new AskedQuestion("Was a Pokemon Knocked Out?", "Yes."), new AskedQuestion("Did they see the card?", "Not known.") },
            state.AskedQuestions);
        Assert.Empty(state.ConfirmedFacts);
    }
}
