namespace PokeJudge.Tests.Clarification;

using PokeJudge.Clarification;
using PokeJudge.StructuredState;
using PokeJudge.Tests.TestDoubles;

public class ClarificationLoopTests
{
    private static readonly MockScenario Scenario = new(
        "Test Scenario",
        "A test scenario description.",
        new[] { new PolicySnippet("X1", "Material snippet text.") });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_MaxTurnsIsNotPositive_ThrowsArgumentOutOfRangeException(int maxTurns)
    {
        var stub = new StubLlmClient();

        Assert.Throws<ArgumentOutOfRangeException>(() => new ClarificationLoop(stub, maxTurns));
    }

    [Fact]
    public async Task RunAsync_FirstAssessmentSufficient_ReturnsImmediatelyWithoutAskingJudge()
    {
        var stub = new StubLlmClient();
        stub.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>(),
            new DraftRuling("Issue a Warning.", new List<string> { "X1" })));

        var loop = new ClarificationLoop(stub);

        var outcome = await loop.RunAsync(
            Scenario,
            askJudge: _ => throw new InvalidOperationException("askJudge should not be called when already sufficient."));

        Assert.True(outcome.Sufficient);
        Assert.Equal(1, outcome.TurnsUsed);
        Assert.Equal("Issue a Warning.", outcome.Draft!.RecommendedAction);
    }

    [Fact]
    public async Task RunAsync_InsufficientThenSufficient_AccumulatesFactsAndHypothesesAcrossTurns()
    {
        var stub = new StubLlmClient();
        stub.Enqueue(new ClarificationResult(false,
            new List<ClarifyingQuestion> { new("Was a Pokemon Knocked Out?", "X1") }, null));
        stub.Enqueue(new FactExtractionResult(
            new List<string> { "A Pokemon was Knocked Out." },
            new List<string> { "The player probably lost track of time." }));
        stub.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>(),
            new DraftRuling("Issue a Warning.", new List<string> { "X1" })));

        var loop = new ClarificationLoop(stub);

        var outcome = await loop.RunAsync(Scenario, askJudge: _ => Task.FromResult("Yes, it was."));

        Assert.True(outcome.Sufficient);
        Assert.Equal(2, outcome.TurnsUsed);
        Assert.Equal(new[] { "A Pokemon was Knocked Out." }, outcome.State.ConfirmedFacts);
        Assert.Equal(new[] { "The player probably lost track of time." }, outcome.State.Hypotheses);

        // Call order: turn-1 sufficiency [0], turn-1 fact extraction [1],
        // turn-2 sufficiency [2]. The second sufficiency call must be given
        // the fact confirmed in turn 1.
        Assert.Contains("A Pokemon was Knocked Out.", stub.UserContents[2]);
    }

    [Fact]
    public async Task RunAsync_NeverSufficientWithinTurnCap_ReturnsTurnCapExhausted()
    {
        var stub = new StubLlmClient();
        for (var i = 0; i < 2; i++)
        {
            stub.Enqueue(new ClarificationResult(false,
                new List<ClarifyingQuestion> { new("Question?", "X1") }, null));
            stub.Enqueue(new FactExtractionResult(new List<string>(), new List<string>()));
        }

        var loop = new ClarificationLoop(stub, maxTurns: 2);

        var outcome = await loop.RunAsync(Scenario, askJudge: _ => Task.FromResult("I don't know."));

        Assert.False(outcome.Sufficient);
        Assert.Null(outcome.Draft);
        Assert.Equal(2, outcome.TurnsUsed);
    }

    [Fact]
    public async Task RunAsync_MultipleQuestionsInOneTurn_AsksAndExtractsEachInOrder()
    {
        var stub = new StubLlmClient();
        stub.Enqueue(new ClarificationResult(false,
            new List<ClarifyingQuestion>
            {
                new("Question 1?", "X1"),
                new("Question 2?", "X1"),
            }, null));
        stub.Enqueue(new FactExtractionResult(new List<string> { "fact from Q1" }, new List<string>()));
        stub.Enqueue(new FactExtractionResult(new List<string> { "fact from Q2" }, new List<string>()));
        stub.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>(),
            new DraftRuling("Action.", new List<string> { "X1" })));

        var askedQuestions = new List<string>();
        var loop = new ClarificationLoop(stub);

        var outcome = await loop.RunAsync(Scenario, askJudge: q =>
        {
            askedQuestions.Add(q.Question);
            return Task.FromResult("answer");
        });

        Assert.Equal(new[] { "Question 1?", "Question 2?" }, askedQuestions);
        Assert.Equal(new[] { "fact from Q1", "fact from Q2" }, outcome.State.ConfirmedFacts);
    }

    [Fact]
    public async Task RunAsync_SufficientWithoutDraft_ThrowsRatherThanReturningNullSilently()
    {
        var stub = new StubLlmClient();
        stub.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>(), null));

        var loop = new ClarificationLoop(stub);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => loop.RunAsync(Scenario, askJudge: _ => Task.FromResult("unused")));
    }

    [Fact]
    public async Task RunAsync_InsufficientWithoutQuestions_ThrowsRatherThanLoopingSilently()
    {
        var stub = new StubLlmClient();
        stub.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion>(), null));

        var loop = new ClarificationLoop(stub);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => loop.RunAsync(Scenario, askJudge: _ => Task.FromResult("unused")));
    }
}
