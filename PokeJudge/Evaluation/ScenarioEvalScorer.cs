namespace PokeJudge.Evaluation;

using PokeJudge.Retrieval;

public enum CriterionResult
{
    Pass,
    Fail
}

public sealed record CriterionOutcome(string Name, CriterionResult Result, string Detail);

public sealed record ScenarioEvalReport(string ScenarioId, IReadOnlyList<CriterionOutcome> Criteria)
{
    public bool AllPassed => Criteria.Count > 0 && Criteria.All(c => c.Result == CriterionResult.Pass);
}

// Pure, deterministic scoring over an already-captured trajectory -- the same role
// SourceSupportAssigner plays in Milestone 7: the thing being scored (a live
// model's behavior) is not deterministic, but the comparison against hand-authored
// expected criteria is, and that comparison is what's unit tested here. Only
// criteria applicable to a scenario's ExpectedOutcome are scored -- an omitted
// criterion is not the same as a passed one.
public static class ScenarioEvalScorer
{
    public static ScenarioEvalReport Score(ScenarioTrajectory trajectory)
    {
        var scenario = trajectory.Scenario;
        var criteria = new List<CriterionOutcome>();

        if (scenario.ExpectedOutcome == ExpectedTrajectoryOutcome.ExpectedToFailLoudly)
        {
            criteria.Add(ScoreExpectedFailure(trajectory));
            return new ScenarioEvalReport(scenario.Id, criteria);
        }

        // A scenario not expected to fail loudly still might, for real (observed running
        // "deck-not-shuffled" against the live corpus: the second turn's malformed
        // insufficient-with-zero-questions response crashed it). Scoring the other
        // criteria against a trajectory truncated by that crash would produce a
        // misleading report (e.g. "Sufficiency timing: a clarifying question was asked"
        // when the real story is the loop crashed) -- flag it plainly instead.
        if (trajectory.ThrewExpectedFailure)
        {
            criteria.Add(new CriterionOutcome("Unexpected failure", CriterionResult.Fail,
                $"The loop failed loudly, which this scenario did not expect: {trajectory.FailureMessage}"));
            return new ScenarioEvalReport(scenario.Id, criteria);
        }

        criteria.Add(ScoreInitialRetrieval(scenario, trajectory));
        criteria.Add(ScoreSufficiencyTiming(scenario, trajectory));

        if (scenario.ExpectedOutcome == ExpectedTrajectoryOutcome.RequiresOneClarification)
        {
            criteria.Add(ScoreClarifyingQuestionMateriality(scenario, trajectory));
            criteria.Add(ScorePostAnswerRetrieval(scenario, trajectory));
            criteria.Add(ScoreAnswerBudget(trajectory));
        }

        if (scenario.AcceptableFinalSourceSupport is { Count: > 0 })
        {
            criteria.Add(ScoreFinalSourceSupport(scenario, trajectory));
        }

        return new ScenarioEvalReport(scenario.Id, criteria);
    }

    private static CriterionOutcome ScoreExpectedFailure(ScenarioTrajectory trajectory) =>
        trajectory.ThrewExpectedFailure
            ? new CriterionOutcome("Expected failure", CriterionResult.Pass, $"Failed loudly as expected: {trajectory.FailureMessage}")
            : new CriterionOutcome("Expected failure", CriterionResult.Fail,
                "Expected the loop to fail loudly (insufficient with no questions), but it completed instead.");

    private static CriterionOutcome ScoreInitialRetrieval(EvalScenario scenario, ScenarioTrajectory trajectory)
    {
        var hit = trajectory.Turns.Count > 0
            && HitsAny(scenario.InitialDescription, scenario.ExpectedMaterialSectionIds, trajectory.Turns[0].RetrievedChunks);

        return hit
            ? new CriterionOutcome("Initial retrieval", CriterionResult.Pass, "Retrieved at least one expected material section on turn 1.")
            : new CriterionOutcome("Initial retrieval", CriterionResult.Fail,
                $"None of the expected section(s) [{string.Join(", ", scenario.ExpectedMaterialSectionIds)}] were retrieved on turn 1.");
    }

    private static CriterionOutcome ScoreSufficiencyTiming(EvalScenario scenario, ScenarioTrajectory trajectory)
    {
        if (trajectory.Turns.Count == 0)
        {
            return new CriterionOutcome("Sufficiency timing", CriterionResult.Fail, "No turns were recorded.");
        }

        var firstTurnSufficient = trajectory.Turns[0].IsSufficient;

        return scenario.ExpectedOutcome switch
        {
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn => firstTurnSufficient
                ? new CriterionOutcome("Sufficiency timing", CriterionResult.Pass, "Resolved without unnecessary clarification, as expected.")
                : new CriterionOutcome("Sufficiency timing", CriterionResult.Fail,
                    "Expected immediate sufficiency, but a clarifying question was asked."),
            ExpectedTrajectoryOutcome.RequiresOneClarification => !firstTurnSufficient
                ? new CriterionOutcome("Sufficiency timing", CriterionResult.Pass, "Correctly recognized the initial scenario was incomplete.")
                : new CriterionOutcome("Sufficiency timing", CriterionResult.Fail,
                    "Expected a clarifying question on turn 1, but the scenario was treated as immediately sufficient (a premature-ruling risk)."),
            _ => new CriterionOutcome("Sufficiency timing", CriterionResult.Fail, $"Unhandled expected outcome: {scenario.ExpectedOutcome}."),
        };
    }

    private static CriterionOutcome ScoreClarifyingQuestionMateriality(EvalScenario scenario, ScenarioTrajectory trajectory)
    {
        var firstTurnQuestions = trajectory.Turns.Count > 0
            ? trajectory.Turns[0].Questions
            : (IReadOnlyList<StructuredState.ClarifyingQuestion>)Array.Empty<StructuredState.ClarifyingQuestion>();

        var materialQuestion = firstTurnQuestions.FirstOrDefault(
            q => scenario.ExpectedMaterialSectionIds.Contains(SectionIdFromChunkId(q.RelatedChunkId)));

        return materialQuestion is not null
            ? new CriterionOutcome("Clarifying question materiality", CriterionResult.Pass,
                $"Question was tied to an expected material section ({materialQuestion.RelatedChunkId}).")
            : new CriterionOutcome("Clarifying question materiality", CriterionResult.Fail,
                $"No clarifying question was tied to any of the expected section(s) [{string.Join(", ", scenario.ExpectedMaterialSectionIds)}].");
    }

    private static CriterionOutcome ScorePostAnswerRetrieval(EvalScenario scenario, ScenarioTrajectory trajectory)
    {
        if (scenario.ExpectedMaterialSectionIdsAfterAnswer.Count == 0)
        {
            return new CriterionOutcome("Post-answer retrieval", CriterionResult.Pass, "No post-answer section(s) specified for this scenario.");
        }

        if (trajectory.Turns.Count < 2)
        {
            return new CriterionOutcome("Post-answer retrieval", CriterionResult.Fail,
                "Expected a second turn after the scripted answer, but none was recorded.");
        }

        var hit = HitsAny(scenario.InitialDescription, scenario.ExpectedMaterialSectionIdsAfterAnswer, trajectory.Turns[1].RetrievedChunks);

        return hit
            ? new CriterionOutcome("Post-answer retrieval", CriterionResult.Pass, "Retrieval after the scripted answer surfaced an expected section.")
            : new CriterionOutcome("Post-answer retrieval", CriterionResult.Fail,
                $"None of the expected post-answer section(s) [{string.Join(", ", scenario.ExpectedMaterialSectionIdsAfterAnswer)}] were retrieved on turn 2.");
    }

    private static CriterionOutcome ScoreAnswerBudget(ScenarioTrajectory trajectory) =>
        trajectory.AskedMoreQuestionsThanScripted
            ? new CriterionOutcome("Answer budget", CriterionResult.Fail,
                "Needed more clarification than the single scripted answer provided.")
            : new CriterionOutcome("Answer budget", CriterionResult.Pass,
                "Resolved within the single scripted answer.");

    private static CriterionOutcome ScoreFinalSourceSupport(EvalScenario scenario, ScenarioTrajectory trajectory)
    {
        if (trajectory.Grounding is null)
        {
            return new CriterionOutcome("Final Source Support", CriterionResult.Fail, "No ruling was produced to evaluate.");
        }

        var actual = trajectory.Grounding.ValidatedSourceSupport;

        return scenario.AcceptableFinalSourceSupport!.Contains(actual)
            ? new CriterionOutcome("Final Source Support", CriterionResult.Pass,
                $"Validated Source Support ({actual}) was in the acceptable set.")
            : new CriterionOutcome("Final Source Support", CriterionResult.Fail,
                $"Validated Source Support was {actual}, not in the acceptable set [{string.Join(", ", scenario.AcceptableFinalSourceSupport)}].");
    }

    private static bool HitsAny(string query, IReadOnlyList<string> expectedSectionIds, IReadOnlyList<ScoredChunk> chunks) =>
        expectedSectionIds.Any(id => RetrievalEvaluator.Evaluate(new RetrievalEvalCase(query, id), chunks).Hit);

    private static string SectionIdFromChunkId(string chunkId)
    {
        var hashIndex = chunkId.IndexOf('#');
        return hashIndex < 0 ? chunkId : chunkId[..hashIndex];
    }
}
