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
        ScriptedAnswers: Array.Empty<string>(), ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
        AcceptableFinalSourceSupport: null);

    private static EvalScenario RequiresOneClarificationScenario() => new(
        "special-condition", "Illegal Game State", "A Special Condition marker looks wrong.",
        new List<string> { "A1" }, ExpectedTrajectoryOutcome.RequiresOneClarification,
        ScriptedAnswers: new[] { "The marker is Asleep, but it should be Confused." },
        ExpectedMaterialSectionIdsAfterAnswer: new List<string> { "A1" },
        AcceptableFinalSourceSupport: null);

    private static EvalScenario RequiresTwoClarificationsScenario() => new(
        "supporter-twice-like", "Timing Questions", "A player thinks their opponent played two Supporter cards.",
        new List<string> { "A1" }, ExpectedTrajectoryOutcome.RequiresOneClarification,
        ScriptedAnswers: new[]
        {
            "Yes, both Supporter cards were fully played and resolved.",
            "The error was noticed three turns later, well after both effects had already taken place.",
        },
        ExpectedMaterialSectionIdsAfterAnswer: new List<string> { "A1" },
        AcceptableFinalSourceSupport: null);

    private static EvalScenario ExpectedFailureScenario() => new(
        "missed-prize", "Prize Errors", "A player forgot to take a Prize card.",
        Array.Empty<string>(), ExpectedTrajectoryOutcome.ExpectedToFailLoudly,
        ScriptedAnswers: Array.Empty<string>(), ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
        AcceptableFinalSourceSupport: null);

    private static (ScenarioEvalRunner Runner, StubLlmClient Llm, StubRetriever Retriever) BuildRunner()
    {
        var llm = new StubLlmClient();
        var retriever = new StubRetriever();
        var loop = new ClarificationLoop(llm, retriever);
        var rulingGenerator = new RulingGenerator(llm);
        var groundingValidator = new GroundingValidator(llm);
        return (new ScenarioEvalRunner(loop, rulingGenerator, groundingValidator), llm, retriever);
    }

    [Fact]
    public async Task RunAsync_SufficientOnFirstTurn_CompletesWithoutAskingTheScriptedAnswer()
    {
        var (runner, llm, retriever) = BuildRunner();
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        retriever.Enqueue(new[] { Chunk("A1") });
        llm.Enqueue(new RulingResult("Rec.", "Expl.", new List<string>(), null, new List<string> { "A1#0" }, SourceSupport.Strong, "n/a"));
        llm.Enqueue(new GroundingAssessment(new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) }, false, "n/a"));

        var trajectory = await runner.RunAsync(SufficientOnFirstTurnScenario());

        Assert.True(trajectory.ReachedSufficiency);
        Assert.Equal(1, trajectory.TurnsUsed);
        Assert.Single(trajectory.Turns);
        Assert.NotNull(trajectory.Ruling);
        Assert.NotNull(trajectory.Grounding);
    }

    [Fact]
    public async Task RunAsync_RequiresOneClarification_UsesTheScriptedAnswerAndReachesSufficiency()
    {
        var (runner, llm, retriever) = BuildRunner();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("What happened?", "A1#0") }));
        llm.Enqueue(new FactExtractionResult(new List<string> { "The marker is Asleep." }, new List<string>()));
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        retriever.Enqueue(new[] { Chunk("A1") });
        retriever.Enqueue(new[] { Chunk("A1") });
        llm.Enqueue(new RulingResult("Rec.", "Expl.", new List<string>(), null, new List<string> { "A1#0" }, SourceSupport.Partial, "n/a"));
        llm.Enqueue(new GroundingAssessment(new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) }, false, "n/a"));

        var trajectory = await runner.RunAsync(RequiresOneClarificationScenario());

        Assert.True(trajectory.ReachedSufficiency);
        Assert.Equal(2, trajectory.Turns.Count);
        Assert.False(trajectory.AskedMoreQuestionsThanScripted);
        Assert.Contains("The marker is Asleep.", llm.UserContents[2]);
    }

    [Fact]
    public async Task RunAsync_RequiresTwoClarifications_ConsumesBothScriptedAnswersInOrderAndReachesSufficiency()
    {
        var (runner, llm, retriever) = BuildRunner();
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

        var trajectory = await runner.RunAsync(RequiresTwoClarificationsScenario());

        Assert.True(trajectory.ReachedSufficiency);
        Assert.Equal(3, trajectory.Turns.Count);
        Assert.False(trajectory.AskedMoreQuestionsThanScripted);
        Assert.Contains("Both Supporter cards resolved.", llm.UserContents[2]);
        Assert.Contains("Noticed three turns later.", llm.UserContents[4]);
    }

    [Fact]
    public async Task RunAsync_LoopAsksMoreQuestionsThanScripted_RecordsItRatherThanCrashing()
    {
        var (runner, llm, retriever) = BuildRunner();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Q1?", "A1#0") }));
        llm.Enqueue(new FactExtractionResult(new List<string>(), new List<string>()));
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Q2?", "A1#0") }));
        llm.Enqueue(new FactExtractionResult(new List<string>(), new List<string>()));
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        retriever.Enqueue(new[] { Chunk("A1") });
        retriever.Enqueue(new[] { Chunk("A1") });
        retriever.Enqueue(new[] { Chunk("A1") });
        llm.Enqueue(new RulingResult("Rec.", "Expl.", new List<string>(), null, new List<string> { "A1#0" }, SourceSupport.Strong, "n/a"));
        llm.Enqueue(new GroundingAssessment(new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) }, false, "n/a"));

        // RequiresOneClarificationScenario only scripts a single answer -- the
        // second question here exceeds it, so it should be flagged, not silently
        // answered with the same single scripted answer again.
        var trajectory = await runner.RunAsync(RequiresOneClarificationScenario());

        Assert.True(trajectory.AskedMoreQuestionsThanScripted);
        Assert.True(trajectory.ReachedSufficiency);
    }

    [Fact]
    public async Task RunAsync_LoopThrowsInsufficientWithNoQuestions_ReturnsAFailedTrajectoryInsteadOfPropagating()
    {
        var (runner, llm, retriever) = BuildRunner();
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
        var (runner, llm, retriever) = BuildRunner();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }));
        retriever.Enqueue(new[] { Chunk("A1") });
        // No FactExtractionResult queued for the scripted answer that follows.

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(RequiresOneClarificationScenario()));
    }

    [Fact]
    public async Task RunAsync_NeverReachesSufficiencyWithinTurnCap_ReturnsTurnCapExhaustedWithoutCallingRulingGenerator()
    {
        var (runner, llm, retriever) = BuildRunner();
        for (var i = 0; i < 4; i++)
        {
            llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }));
            llm.Enqueue(new FactExtractionResult(new List<string>(), new List<string>()));
            retriever.Enqueue(new[] { Chunk("A1") });
        }

        var trajectory = await runner.RunAsync(RequiresOneClarificationScenario());

        Assert.False(trajectory.ReachedSufficiency);
        Assert.False(trajectory.ThrewExpectedFailure);
        Assert.Null(trajectory.Ruling);
        Assert.Equal(4, trajectory.TurnsUsed);
    }
}
