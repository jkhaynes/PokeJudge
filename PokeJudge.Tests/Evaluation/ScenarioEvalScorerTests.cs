namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Chunking;
using PokeJudge.Evaluation;
using PokeJudge.Grounding;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

public class ScenarioEvalScorerTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string sectionId, double score = 0.8) =>
        new(new EmbeddedChunk(new TextChunk($"{sectionId}#0", sectionId, $"Text for {sectionId}", Source), new float[] { 1f }), score);

    private static EvalScenario SufficientOnFirstTurnScenario(IReadOnlyList<string>? expectedSections = null) => new(
        "notes", "Tournament Procedure", "Is a competitor allowed to keep written notes?",
        expectedSections ?? new List<string> { "A1" },
        ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
        ScriptedAnswers: Array.Empty<string>(),
        ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
        AcceptableFinalSourceSupport: null);

    private static EvalScenario RequiresOneClarificationScenario(
        IReadOnlyList<string>? afterAnswerSections = null, IReadOnlySet<SourceSupport>? acceptable = null) => new(
        "special-condition", "Illegal Game State", "A Special Condition marker looks wrong.",
        new List<string> { "A1" },
        ExpectedTrajectoryOutcome.RequiresOneClarification,
        ScriptedAnswers: new[] { "The marker is Asleep, but it should be Confused." },
        afterAnswerSections ?? new List<string> { "A1" },
        acceptable);

    private static EvalScenario ExpectedFailureScenario() => new(
        "missed-prize", "Prize Errors", "A player forgot to take a Prize card.",
        Array.Empty<string>(),
        ExpectedTrajectoryOutcome.ExpectedToFailLoudly,
        ScriptedAnswers: Array.Empty<string>(),
        ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
        AcceptableFinalSourceSupport: null);

    private static EvalScenario ExpectedUnresolvableScenario() => new(
        "missed-prize", "Prize Errors", "A player forgot to take a Prize card.",
        Array.Empty<string>(),
        ExpectedTrajectoryOutcome.ExpectedUnresolvable,
        ScriptedAnswers: Array.Empty<string>(),
        ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
        AcceptableFinalSourceSupport: null);

    private static RulingResult SomeRuling() => new(
        "Recommendation.", "Explanation.", new List<string>(), null, new List<string> { "A1#0" },
        SourceSupport.Strong, "Model's own opinion.");

    private static GroundingResult SomeGrounding(SourceSupport validated) => new(
        validated, "Validated rationale.",
        new GroundingAssessment(new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) }, false, "n/a"),
        RetrievalNonEmpty: true, AllCitationsExist: true, FactsWereSufficient: true);

    // --- ExpectedToFailLoudly ---

    [Fact]
    public void Score_ExpectedToFailLoudly_ActuallyThrew_Passes()
    {
        var trajectory = ScenarioTrajectory.Failed(ExpectedFailureScenario(), new List<TurnRecord>(), "Model reported insufficient with no questions.");

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.True(report.AllPassed);
        Assert.Single(report.Criteria);
    }

    [Fact]
    public void Score_ExpectedToFailLoudly_DidNotThrow_Fails()
    {
        var scenario = ExpectedFailureScenario();
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.False(report.AllPassed);
    }

    [Fact]
    public void Score_NotExpectedToFailButDidAnyway_ReportsUnexpectedFailureRatherThanScoringATruncatedTrajectory()
    {
        // Observed for real running "deck-not-shuffled" (SufficientOnFirstTurn) against
        // the live corpus: it crashed on a later turn's malformed response. Scoring the
        // other criteria against that truncated trajectory would have produced a
        // misleading report instead of surfacing the crash itself.
        var scenario = SufficientOnFirstTurnScenario();
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Failed(scenario, turns, "Model reported insufficient with no questions.");

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.False(report.AllPassed);
        Assert.Single(report.Criteria);
        Assert.Equal("Unexpected failure", report.Criteria[0].Name);
        Assert.Equal(CriterionResult.Fail, report.Criteria[0].Result);
    }

    // --- ExpectedUnresolvable ---

    [Fact]
    public void Score_ExpectedUnresolvable_TurnCapExhausted_Passes()
    {
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }) };
        var trajectory = ScenarioTrajectory.TurnCapExhausted(ExpectedUnresolvableScenario(), turns, 4, true);

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.True(report.AllPassed);
        Assert.Single(report.Criteria);
        Assert.Equal("Expected unresolvable", report.Criteria[0].Name);
    }

    [Fact]
    public void Score_ExpectedUnresolvable_CrashedInstead_Fails()
    {
        var trajectory = ScenarioTrajectory.Failed(
            ExpectedUnresolvableScenario(), new List<TurnRecord>(), "Model reported insufficient with no questions.");

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.False(report.AllPassed);
        Assert.Contains(report.Criteria, c => c.Name == "Expected unresolvable" && c.Result == CriterionResult.Fail);
    }

    [Fact]
    public void Score_ExpectedUnresolvable_ReachedSufficiencyInstead_Fails()
    {
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(
            ExpectedUnresolvableScenario(), turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.False(report.AllPassed);
        Assert.Contains(report.Criteria, c => c.Name == "Expected unresolvable" && c.Result == CriterionResult.Fail);
    }

    // --- Initial retrieval ---

    [Fact]
    public void Score_SufficientOnFirstTurn_ExpectedSectionRetrieved_InitialRetrievalPasses()
    {
        var scenario = SufficientOnFirstTurnScenario(new[] { "A1" });
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Initial retrieval" && c.Result == CriterionResult.Pass);
    }

    [Fact]
    public void Score_SufficientOnFirstTurn_ExpectedSectionNotRetrieved_InitialRetrievalFails()
    {
        var scenario = SufficientOnFirstTurnScenario(new[] { "A1" });
        var turns = new List<TurnRecord> { new(new[] { Chunk("Z9") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Initial retrieval" && c.Result == CriterionResult.Fail);
        Assert.False(report.AllPassed);
    }

    // --- Sufficiency timing ---

    [Fact]
    public void Score_SufficientOnFirstTurn_TurnOneWasSufficient_SufficiencyTimingPasses()
    {
        var scenario = SufficientOnFirstTurnScenario(new[] { "A1" });
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Sufficiency timing" && c.Result == CriterionResult.Pass);
    }

    [Fact]
    public void Score_SufficientOnFirstTurn_TurnOneWasNotSufficient_SufficiencyTimingFails()
    {
        var scenario = SufficientOnFirstTurnScenario(new[] { "A1" });
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }),
            new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 2, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Sufficiency timing" && c.Result == CriterionResult.Fail);
    }

    [Fact]
    public void Score_RequiresOneClarification_TurnOneWasNotSufficient_SufficiencyTimingPasses()
    {
        var scenario = RequiresOneClarificationScenario();
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }),
            new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 2, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Sufficiency timing" && c.Result == CriterionResult.Pass);
    }

    [Fact]
    public void Score_RequiresOneClarification_TurnOneWasSufficient_SufficiencyTimingFails()
    {
        // A premature ruling: the loop treated the scenario as immediately sufficient
        // when the scenario expected at least one clarifying round.
        var scenario = RequiresOneClarificationScenario();
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Sufficiency timing" && c.Result == CriterionResult.Fail);
    }

    // --- Clarifying question materiality ---

    [Fact]
    public void Score_RequiresOneClarification_QuestionTiedToExpectedSection_MaterialityPasses()
    {
        var scenario = RequiresOneClarificationScenario();
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("What happened?", "A1#0") }),
            new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 2, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Clarifying question materiality" && c.Result == CriterionResult.Pass);
    }

    [Fact]
    public void Score_RequiresOneClarification_QuestionNotTiedToExpectedSection_MaterialityFails()
    {
        var scenario = RequiresOneClarificationScenario();
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("What happened?", "Z9#0") }),
            new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 2, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Clarifying question materiality" && c.Result == CriterionResult.Fail);
    }

    // --- Post-answer retrieval ---

    [Fact]
    public void Score_RequiresOneClarification_SecondTurnRetrievesExpectedSection_PostAnswerRetrievalPasses()
    {
        var scenario = RequiresOneClarificationScenario(afterAnswerSections: new[] { "A1" });
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }),
            new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 2, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Post-answer retrieval" && c.Result == CriterionResult.Pass);
    }

    [Fact]
    public void Score_RequiresOneClarification_SecondTurnMissesExpectedSection_PostAnswerRetrievalFails()
    {
        var scenario = RequiresOneClarificationScenario(afterAnswerSections: new[] { "A1" });
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }),
            new(new[] { Chunk("Z9") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 2, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Post-answer retrieval" && c.Result == CriterionResult.Fail);
    }

    [Fact]
    public void Score_RequiresOneClarification_NoAfterAnswerSectionsSpecified_PostAnswerRetrievalAutoPasses()
    {
        var scenario = RequiresOneClarificationScenario(afterAnswerSections: Array.Empty<string>());
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }),
            new(new[] { Chunk("Z9") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 2, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Post-answer retrieval" && c.Result == CriterionResult.Pass);
    }

    // --- Answer budget ---

    [Fact]
    public void Score_RequiresOneClarification_AskedMoreQuestionsThanScripted_AnswerBudgetFails()
    {
        var scenario = RequiresOneClarificationScenario();
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }),
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q2?", "A1#0") }),
            new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 3, true, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Answer budget" && c.Result == CriterionResult.Fail);
        Assert.False(report.AllPassed);
    }

    [Fact]
    public void Score_RequiresOneClarification_ResolvedWithinScriptedAnswer_AnswerBudgetPasses()
    {
        var scenario = RequiresOneClarificationScenario();
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }),
            new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 2, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Answer budget" && c.Result == CriterionResult.Pass);
    }

    [Fact]
    public void Score_SufficientOnFirstTurn_AnswerBudgetCriterionOmitted()
    {
        var scenario = SufficientOnFirstTurnScenario(new[] { "A1" });
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.DoesNotContain(report.Criteria, c => c.Name == "Answer budget");
    }

    [Fact]
    public void Score_ExpectedToFailLoudly_AnswerBudgetCriterionOmitted()
    {
        var trajectory = ScenarioTrajectory.Failed(ExpectedFailureScenario(), new List<TurnRecord>(), "Model reported insufficient with no questions.");

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.DoesNotContain(report.Criteria, c => c.Name == "Answer budget");
    }

    // --- Final Source Support ---

    [Fact]
    public void Score_ValidatedSourceSupportInAcceptableSet_FinalSourceSupportPasses()
    {
        var scenario = SufficientOnFirstTurnScenario(new[] { "A1" }) with
        {
            AcceptableFinalSourceSupport = new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial },
        };
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Partial));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Final Source Support" && c.Result == CriterionResult.Pass);
    }

    [Fact]
    public void Score_ValidatedSourceSupportNotInAcceptableSet_FinalSourceSupportFails()
    {
        var scenario = SufficientOnFirstTurnScenario(new[] { "A1" }) with
        {
            AcceptableFinalSourceSupport = new HashSet<SourceSupport> { SourceSupport.Strong },
        };
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Insufficient));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Final Source Support" && c.Result == CriterionResult.Fail);
    }

    [Fact]
    public void Score_NoAcceptableFinalSourceSupportSpecified_FinalSourceSupportCriterionOmitted()
    {
        var scenario = SufficientOnFirstTurnScenario(new[] { "A1" });
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, false, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.DoesNotContain(report.Criteria, c => c.Name == "Final Source Support");
    }

    // --- ScenarioEvalReport.AllPassed ---

    [Fact]
    public void AllPassed_NoCriteria_IsFalse()
    {
        // Score() never actually produces zero criteria today, but AllPassed's own
        // guard (Criteria.Count > 0) is real logic worth locking in directly: an empty
        // criteria list must not vacuously count as "all passed," the same principle
        // Milestone 7's AllCitationsExist applied to an empty citation list.
        var report = new ScenarioEvalReport("empty", new List<CriterionOutcome>());

        Assert.False(report.AllPassed);
    }

    [Fact]
    public void Score_TurnCapExhaustedButFinalSourceSupportExpected_FinalSourceSupportFails()
    {
        var scenario = RequiresOneClarificationScenario() with
        {
            AcceptableFinalSourceSupport = new HashSet<SourceSupport> { SourceSupport.Strong },
        };
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }) };
        var trajectory = ScenarioTrajectory.TurnCapExhausted(scenario, turns, 4, false);

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Final Source Support" && c.Result == CriterionResult.Fail);
    }
}

