namespace PokeJudge.Tests.Clarification;

using PokeJudge.Chunking;
using PokeJudge.Clarification;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;
using PokeJudge.Tests.TestDoubles;

public class ClarificationLoopTests
{
    private const string ScenarioDescription = "A test scenario description.";
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string chunkId, double score = 0.8) =>
        new(new EmbeddedChunk(new TextChunk(chunkId, chunkId, $"Text for {chunkId}", Source), new float[] { 1f }), score);

    private static IReadOnlyList<ScoredChunk> SomeChunks() => new[] { Chunk("X1#0") };

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_MaxTurnsIsNotPositive_ThrowsArgumentOutOfRangeException(int maxTurns)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ClarificationLoop(new StubLlmClient(), new StubRetriever(), maxTurns));
    }

    [Fact]
    public async Task RunAsync_FirstAssessmentSufficient_ReturnsImmediatelyWithoutAskingJudge()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        var retriever = new StubRetriever();
        retriever.Enqueue(SomeChunks());

        var loop = new ClarificationLoop(llm, retriever);

        var outcome = await loop.RunAsync(
            ScenarioDescription,
            askJudge: _ => throw new InvalidOperationException("askJudge should not be called when already sufficient."));

        Assert.True(outcome.Sufficient);
        Assert.Equal(1, outcome.TurnsUsed);
    }

    [Fact]
    public async Task RunAsync_RetrievesBeforeTheFirstAssessment()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        var retriever = new StubRetriever();
        retriever.Enqueue(SomeChunks());

        var loop = new ClarificationLoop(llm, retriever);

        await loop.RunAsync(ScenarioDescription, askJudge: _ => throw new InvalidOperationException());

        Assert.Single(retriever.QueryTexts);
        Assert.Contains(ScenarioDescription, retriever.QueryTexts[0]);
    }

    [Fact]
    public async Task RunAsync_InsufficientThenSufficient_AccumulatesFactsAndHypothesesAcrossTurns()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new ClarificationResult(false,
            new List<ClarifyingQuestion> { new("Was a Pokemon Knocked Out?", "X1#0") }));
        llm.Enqueue(new FactExtractionResult(
            new List<string> { "A Pokemon was Knocked Out." },
            new List<string> { "The player probably lost track of time." }));
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));

        var retriever = new StubRetriever();
        retriever.Enqueue(SomeChunks());
        retriever.Enqueue(SomeChunks());

        var loop = new ClarificationLoop(llm, retriever);

        var outcome = await loop.RunAsync(ScenarioDescription, askJudge: _ => Task.FromResult("Yes, it was."));

        Assert.True(outcome.Sufficient);
        Assert.Equal(2, outcome.TurnsUsed);
        Assert.Equal(new[] { "A Pokemon was Knocked Out." }, outcome.State.ConfirmedFacts);
        Assert.Equal(new[] { "The player probably lost track of time." }, outcome.State.Hypotheses);

        // Call order: turn-1 sufficiency [0], turn-1 fact extraction [1],
        // turn-2 sufficiency [2]. The second sufficiency call must be given
        // the fact confirmed in turn 1.
        Assert.Contains("A Pokemon was Knocked Out.", llm.UserContents[2]);
    }

    [Fact]
    public async Task RunAsync_ReRetrievesEachTurn_WithAQueryThatGrowsAsFactsAreConfirmed()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new ClarificationResult(false,
            new List<ClarifyingQuestion> { new("Was a Pokemon Knocked Out?", "X1#0") }));
        llm.Enqueue(new FactExtractionResult(
            new List<string> { "A Pokemon was Knocked Out." }, new List<string>()));
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));

        var retriever = new StubRetriever();
        retriever.Enqueue(SomeChunks());
        retriever.Enqueue(SomeChunks());

        var loop = new ClarificationLoop(llm, retriever);

        await loop.RunAsync(ScenarioDescription, askJudge: _ => Task.FromResult("Yes, it was."));

        Assert.Equal(2, retriever.QueryTexts.Count);
        Assert.DoesNotContain("A Pokemon was Knocked Out.", retriever.QueryTexts[0]);
        Assert.Contains("A Pokemon was Knocked Out.", retriever.QueryTexts[1]);
        Assert.True(retriever.QueryTexts[1].Length > retriever.QueryTexts[0].Length);
    }

    [Fact]
    public async Task RunAsync_NeverSufficientWithinTurnCap_ReturnsTurnCapExhausted()
    {
        var llm = new StubLlmClient();
        var retriever = new StubRetriever();
        for (var i = 0; i < 2; i++)
        {
            llm.Enqueue(new ClarificationResult(false,
                new List<ClarifyingQuestion> { new("Question?", "X1#0") }));
            llm.Enqueue(new FactExtractionResult(new List<string>(), new List<string>()));
            retriever.Enqueue(SomeChunks());
        }

        var loop = new ClarificationLoop(llm, retriever, maxTurns: 2);

        var outcome = await loop.RunAsync(ScenarioDescription, askJudge: _ => Task.FromResult("I don't know."));

        Assert.False(outcome.Sufficient);
        Assert.Equal(2, outcome.TurnsUsed);
    }

    [Fact]
    public async Task RunAsync_MultipleQuestionsInOneTurn_AsksAndExtractsEachInOrder()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new ClarificationResult(false,
            new List<ClarifyingQuestion>
            {
                new("Question 1?", "X1#0"),
                new("Question 2?", "X1#0"),
            }));
        llm.Enqueue(new FactExtractionResult(new List<string> { "fact from Q1" }, new List<string>()));
        llm.Enqueue(new FactExtractionResult(new List<string> { "fact from Q2" }, new List<string>()));
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));

        var retriever = new StubRetriever();
        retriever.Enqueue(SomeChunks());
        retriever.Enqueue(SomeChunks());

        var askedQuestions = new List<string>();
        var loop = new ClarificationLoop(llm, retriever);

        var outcome = await loop.RunAsync(ScenarioDescription, askJudge: q =>
        {
            askedQuestions.Add(q.Question);
            return Task.FromResult("answer");
        });

        Assert.Equal(new[] { "Question 1?", "Question 2?" }, askedQuestions);
        Assert.Equal(new[] { "fact from Q1", "fact from Q2" }, outcome.State.ConfirmedFacts);
    }

    [Fact]
    public async Task RunAsync_InsufficientWithoutQuestions_ThrowsRatherThanLoopingSilently()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion>()));
        var retriever = new StubRetriever();
        retriever.Enqueue(SomeChunks());

        var loop = new ClarificationLoop(llm, retriever);

        await Assert.ThrowsAsync<InsufficientWithoutQuestionsException>(
            () => loop.RunAsync(ScenarioDescription, askJudge: _ => Task.FromResult("unused")));
    }

    [Fact]
    public async Task RunAsync_InsufficientWithoutQuestions_ExceptionMessageIncludesModelsRationale()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new ClarificationResult(
            false, new List<ClarifyingQuestion>(), "No retrieved passage's applicability turns on any missing fact."));
        var retriever = new StubRetriever();
        retriever.Enqueue(SomeChunks());

        var loop = new ClarificationLoop(llm, retriever);

        var ex = await Assert.ThrowsAsync<InsufficientWithoutQuestionsException>(
            () => loop.RunAsync(ScenarioDescription, askJudge: _ => Task.FromResult("unused")));

        Assert.Contains("No retrieved passage's applicability turns on any missing fact.", ex.Message);
    }

    [Fact]
    public async Task RunAsync_InsufficientWithoutQuestionsAndNoRationale_ExceptionMessageNotesNoneGiven()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion>(), Rationale: ""));
        var retriever = new StubRetriever();
        retriever.Enqueue(SomeChunks());

        var loop = new ClarificationLoop(llm, retriever);

        var ex = await Assert.ThrowsAsync<InsufficientWithoutQuestionsException>(
            () => loop.RunAsync(ScenarioDescription, askJudge: _ => Task.FromResult("unused")));

        Assert.Contains("(none given)", ex.Message);
    }

    [Fact]
    public async Task RunAsync_OnAssessmentCallback_ReceivesTheChunksRetrievedForThatTurn()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        var retriever = new StubRetriever();
        var chunksForTurn = SomeChunks();
        retriever.Enqueue(chunksForTurn);

        var loop = new ClarificationLoop(llm, retriever);

        IReadOnlyList<ScoredChunk>? observed = null;
        await loop.RunAsync(
            ScenarioDescription,
            askJudge: _ => throw new InvalidOperationException(),
            onAssessment: (_, chunks) => observed = chunks);

        Assert.Same(chunksForTurn, observed);
    }
}
