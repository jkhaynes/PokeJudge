namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Chunking;
using PokeJudge.Clarification;
using PokeJudge.Evaluation;
using PokeJudge.Grounding;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;
using PokeJudge.Tests.TestDoubles;

public class ScenarioEvalRunnerTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string sectionId, double score = 0.8) =>
        new(new EmbeddedChunk(new TextChunk($"{sectionId}#0", sectionId, $"Text for {sectionId}", Source), new float[] { 1f }), score);

    private static EvalScenario SufficientOnFirstTurnScenario() => new(
        "notes", "Tournament Procedure", "Is a competitor allowed to keep written notes?",
        new List<string> { "A1" }, ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
        FactSheet: "Test facts.", ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
        AcceptableFinalSourceSupport: null);

    private static EvalScenario RequiresOneClarificationScenario() => new(
        "special-condition", "Illegal Game State", "A Special Condition marker looks wrong.",
        new List<string> { "A1" }, ExpectedTrajectoryOutcome.RequiresOneClarification,
        FactSheet: "The marker is Asleep, but it should be Confused.",
        ExpectedMaterialSectionIdsAfterAnswer: new List<string> { "A1" },
        AcceptableFinalSourceSupport: null);

    private static EvalScenario RequiresTwoClarificationsScenario() => new(
        "supporter-twice-like", "Timing Questions", "A player thinks their opponent played two Supporter cards.",
        new List<string> { "A1" }, ExpectedTrajectoryOutcome.RequiresOneClarification,
        FactSheet: "Both Supporter cards resolved. Noticed three turns later.",
        ExpectedMaterialSectionIdsAfterAnswer: new List<string> { "A1" },
        AcceptableFinalSourceSupport: null);

    private static EvalScenario ExpectedFailureScenario() => new(
        "missed-prize", "Prize Errors", "A player forgot to take a Prize card.",
        Array.Empty<string>(), ExpectedTrajectoryOutcome.ExpectedToFailLoudly,
        FactSheet: "Test facts.", ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
        AcceptableFinalSourceSupport: null);

    // The judge gets its own stub so its answers never mix with the loop's queued results.
    private static (ScenarioEvalRunner Runner, StubLlmClient Llm, StubRetriever Retriever, StubLlmClient JudgeLlm) BuildRunner()
    {
        var llm = new StubLlmClient();
        var judgeLlm = new StubLlmClient();
        var retriever = new StubRetriever();
        var loop = new ClarificationLoop(llm, retriever);
        var rulingGenerator = new RulingGenerator(llm);
        var groundingValidator = new GroundingValidator(llm);
        var judge = new SimulatedJudge(judgeLlm);
        return (new ScenarioEvalRunner(loop, rulingGenerator, groundingValidator, judge), llm, retriever, judgeLlm);
    }

    [Fact]
    public async Task RunAsync_SufficientOnFirstTurn_CompletesWithoutAskingTheJudge()
    {
        var (runner, llm, retriever, _) = BuildRunner();
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        retriever.Enqueue(new[] { Chunk("A1") });
        llm.Enqueue(new RulingResult("Rec.", "Expl.", new List<string>(), null, new List<string> { "A1#0" }, SourceSupport.Strong, "n/a"));
        llm.Enqueue(new GroundingAssessment(new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) }, false, "n/a"));

        var trajectory = await runner.RunAsync(SufficientOnFirstTurnScenario());

        Assert.True(trajectory.ReachedSufficiency);
        Assert.Equal(1, trajectory.TurnsUsed);
        Assert.Single(trajectory.Turns);
        Assert.Empty(trajectory.Exchanges);
        Assert.NotNull(trajectory.Ruling);
        Assert.NotNull(trajectory.Grounding);
    }

    [Fact]
    public async Task RunAsync_RequiresOneClarification_AnswersFromTheJudgeAndReachesSufficiency()
    {
        var (runner, llm, retriever, judgeLlm) = BuildRunner();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("What happened?", "A1#0") }));
        llm.Enqueue(new FactExtractionResult(new List<string> { "The marker is Asleep." }, new List<string>()));
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        retriever.Enqueue(new[] { Chunk("A1") });
        retriever.Enqueue(new[] { Chunk("A1") });
        llm.Enqueue(new RulingResult("Rec.", "Expl.", new List<string>(), null, new List<string> { "A1#0" }, SourceSupport.Partial, "n/a"));
        llm.Enqueue(new GroundingAssessment(new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) }, false, "n/a"));
        judgeLlm.Enqueue(new JudgeAnswer("The marker is Asleep.", true));

        var trajectory = await runner.RunAsync(RequiresOneClarificationScenario());

        Assert.True(trajectory.ReachedSufficiency);
        Assert.Equal(2, trajectory.Turns.Count);
        var exchange = Assert.Single(trajectory.Exchanges);
        Assert.Equal("What happened?", exchange.Question);
        Assert.Equal("The marker is Asleep.", exchange.Answer);
        Assert.Contains("The marker is Asleep.", llm.UserContents[1]);
        Assert.Contains("A Special Condition marker looks wrong.", judgeLlm.UserContents[0]);
        Assert.Contains("The marker is Asleep, but it should be Confused.", judgeLlm.UserContents[0]);
    }

    [Fact]
    public async Task RunAsync_TwoRounds_RecordsBothExchangesInOrder()
    {
        var (runner, llm, retriever, judgeLlm) = BuildRunner();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Q1?", "A1#0") }));
        llm.Enqueue(new FactExtractionResult(new List<string> { "Both Supporter cards resolved." }, new List<string>()));
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Q2?", "A1#0") }));
        llm.Enqueue(new FactExtractionResult(new List<string> { "Noticed three turns later." }, new List<string>()));
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        retriever.Enqueue(new[] { Chunk("A1") });
        retriever.Enqueue(new[] { Chunk("A1") });
        retriever.Enqueue(new[] { Chunk("A1") });
        llm.Enqueue(new RulingResult("Rec.", "Expl.", new List<string>(), null, new List<string> { "A1#0" }, SourceSupport.Strong, "n/a"));
        llm.Enqueue(new GroundingAssessment(new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) }, false, "n/a"));
        judgeLlm.Enqueue(new JudgeAnswer("Both Supporter cards resolved.", true));
        judgeLlm.Enqueue(new JudgeAnswer("Noticed three turns later.", true));

        var trajectory = await runner.RunAsync(RequiresTwoClarificationsScenario());

        Assert.True(trajectory.ReachedSufficiency);
        Assert.Equal(3, trajectory.Turns.Count);
        Assert.Equal(new[] { "Q1?", "Q2?" }, trajectory.Exchanges.Select(e => e.Question));
        Assert.Equal(2, trajectory.ClarifyingRounds);
        Assert.Contains("Both Supporter cards resolved.", llm.UserContents[1]);
        Assert.Contains("Noticed three turns later.", llm.UserContents[3]);
    }

    [Fact]
    public async Task RunAsync_JudgeCannotAnswer_RecordsNotKnownAndPassesItToTheLoop()
    {
        var (runner, llm, retriever, judgeLlm) = BuildRunner();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Which turn?", "A1#0") }));
        llm.Enqueue(new FactExtractionResult(new List<string>(), new List<string>()));
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        retriever.Enqueue(new[] { Chunk("A1") });
        retriever.Enqueue(new[] { Chunk("A1") });
        llm.Enqueue(new RulingResult("Rec.", "Expl.", new List<string>(), null, new List<string> { "A1#0" }, SourceSupport.Strong, "n/a"));
        llm.Enqueue(new GroundingAssessment(new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) }, false, "n/a"));
        judgeLlm.Enqueue(new JudgeAnswer("Maybe turn 2?", false));

        var trajectory = await runner.RunAsync(RequiresOneClarificationScenario());

        Assert.Equal(1, trajectory.NotKnownCount);
        Assert.Equal(SimulatedJudge.NotKnown, trajectory.Exchanges[0].Answer);
        Assert.Contains(SimulatedJudge.NotKnown, llm.UserContents[1]);
        Assert.DoesNotContain("Maybe turn 2?", llm.UserContents[1]);
    }

    [Fact]
    public async Task RunAsync_LoopThrowsInsufficientWithNoQuestions_ReturnsAFailedTrajectoryInsteadOfPropagating()
    {
        var (runner, llm, retriever, _) = BuildRunner();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion>()));
        retriever.Enqueue(new[] { Chunk("A1") });

        var trajectory = await runner.RunAsync(ExpectedFailureScenario());

        Assert.True(trajectory.ThrewExpectedFailure);
        Assert.False(trajectory.ReachedSufficiency);
        Assert.NotNull(trajectory.FailureMessage);
    }

    [Fact]
    public async Task RunAsync_UnrelatedInvalidOperationException_PropagatesRatherThanBeingScoredAsAnExpectedFailure()
    {
        // Regression test for the PR review's Major finding: before
        // InsufficientWithoutQuestionsException existed, ScenarioEvalRunner caught
        // InvalidOperationException broadly, so an unrelated failure here (simulated by
        // StubLlmClient's own "no more queued results" guard, itself a generic
        // InvalidOperationException, structurally identical to a malformed/null
        // structured response) would have been silently misreported as the known
        // zero-questions bug reproducing. It must now propagate instead.
        var (runner, llm, retriever, judgeLlm) = BuildRunner();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }));
        retriever.Enqueue(new[] { Chunk("A1") });
        judgeLlm.Enqueue(new JudgeAnswer("An answer.", true));
        // No FactExtractionResult queued for the judge's answer that follows.

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(RequiresOneClarificationScenario()));
    }

    [Fact]
    public async Task RunAsync_NeverReachesSufficiencyWithinTurnCap_ReturnsTurnCapExhaustedWithoutCallingRulingGenerator()
    {
        var (runner, llm, retriever, judgeLlm) = BuildRunner();
        for (var i = 0; i < 4; i++)
        {
            llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }));
            llm.Enqueue(new FactExtractionResult(new List<string>(), new List<string>()));
            retriever.Enqueue(new[] { Chunk("A1") });
            judgeLlm.Enqueue(new JudgeAnswer("An answer.", true));
        }

        var trajectory = await runner.RunAsync(RequiresOneClarificationScenario());

        Assert.False(trajectory.ReachedSufficiency);
        Assert.False(trajectory.ThrewExpectedFailure);
        Assert.Null(trajectory.Ruling);
        Assert.Equal(4, trajectory.TurnsUsed);
        Assert.Equal(4, trajectory.Exchanges.Count);
    }
}
