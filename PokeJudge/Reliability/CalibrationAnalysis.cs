namespace PokeJudge.Reliability;

using PokeJudge.Evaluation;
using PokeJudge.Grounding;
using PokeJudge.StructuredState;

// One (self-reported confidence, actual outcome) pair captured from a real
// ScenarioTrajectory that reached a scored ruling. Criteria is the full
// ScenarioEvalReport breakdown, not just the collapsed ActualCorrect boolean --
// a miscalibration finding should be traceable to which criterion failed
// (retrieval, materiality, Source Support, etc.), not just flagged as "wrong"
// (Milestone 9 plan.md, step 4).
//
// ValidatedSourceSupport/AllCitationsExist/ConflictDetected/citation-support
// counts added per plan.md step 6/"What We Will Build" item 4 -- comparing
// self-reported confidence against the pipeline's other reliability signals
// (retrieval quality, citation coverage, explicit-vs-inferred support, source
// conflict) requires those signals actually be captured somewhere. Milestone
// 9's review found this data was never captured at all (only Criteria/Category
// were, and even those went unread -- see review.md Must Fix #1); this closes
// the gap for future data-gathering runs. Still a human judgment call, written
// up narratively in calibration-analysis.md, not a new automated scoring
// criterion -- these fields exist to be read by a person, not by more code.
public sealed record CalibrationObservation(
    string ScenarioId,
    string Category,
    int PredictedProbability,
    bool ActualCorrect,
    IReadOnlyList<CriterionOutcome> Criteria,
    SourceSupport ValidatedSourceSupport,
    bool AllCitationsExist,
    bool ConflictDetected,
    int ExplicitSupportCitationCount,
    int InterpretationCitationCount,
    int UnsupportedCitationCount);

public sealed record CalibrationBucket(
    int LowerBound,
    int UpperBound,
    int Count,
    double MeanPredictedProbability,
    double ObservedCorrectRate);

public sealed record CategoryCalibrationSummary(
    string Category,
    int Count,
    double MeanPredictedProbability,
    double ObservedCorrectRate);

public sealed record CriterionFailureCount(string CriterionName, int FailureCount);

// Pure, deterministic analysis over already-captured observations -- no LLM
// calls, the same non-determinism/determinism boundary ScenarioEvalScorer
// established in Milestone 8: the thing being analyzed (live model confidence)
// isn't deterministic, but the comparison against actual outcomes is, and
// that comparison is what's unit tested here.
public static class CalibrationAnalysis
{
    // missed-prize (a documented possible source gap) and mulligan-not-taken
    // (documented, unresolved multi-path variability) both have known, already-
    // explained Milestone 8.5 findings unrelated to confidence calibration --
    // tagging them lets the analysis run with and without so a known issue
    // isn't misread as new calibration evidence (plan.md's addendum).
    public static readonly IReadOnlySet<string> KnownIssueScenarioIds =
        new HashSet<string> { "missed-prize", "mulligan-not-taken" };

    public static IReadOnlyList<CalibrationObservation> ExcludeKnownIssues(
        IReadOnlyList<CalibrationObservation> observations) =>
        observations.Where(o => !KnownIssueScenarioIds.Contains(o.ScenarioId)).ToList();

    public static IReadOnlyList<CalibrationBucket> Bucket(IReadOnlyList<CalibrationObservation> observations, int bucketCount)
    {
        if (bucketCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount), bucketCount, "bucketCount must be at least 1.");
        }

        var width = 100.0 / bucketCount;
        var grouped = new List<CalibrationObservation>[bucketCount];
        for (var i = 0; i < bucketCount; i++)
        {
            grouped[i] = new List<CalibrationObservation>();
        }

        foreach (var observation in observations)
        {
            // Defensive normalization, not full validation: the model is schema-instructed
            // to always return 0-100, but one malformed value shouldn't discard an entire
            // live run's worth of other, valid observations.
            var clamped = Math.Clamp(observation.PredictedProbability, 0, 100);
            var index = Math.Min(bucketCount - 1, (int)(clamped / width));
            grouped[index].Add(observation);
        }

        var buckets = new List<CalibrationBucket>();
        for (var i = 0; i < bucketCount; i++)
        {
            var lower = (int)Math.Round(i * width);
            var upper = (int)Math.Round((i + 1) * width);
            var items = grouped[i];

            var meanPredicted = items.Count > 0 ? items.Average(o => o.PredictedProbability) : 0.0;
            var observedRate = items.Count > 0 ? items.Count(o => o.ActualCorrect) / (double)items.Count : 0.0;

            buckets.Add(new CalibrationBucket(lower, upper, items.Count, meanPredicted, observedRate));
        }

        return buckets;
    }

    public static double BrierScore(IReadOnlyList<CalibrationObservation> observations)
    {
        if (observations.Count == 0)
        {
            throw new ArgumentException("Cannot compute a Brier score over zero observations.", nameof(observations));
        }

        var sumSquaredError = observations.Sum(o =>
        {
            var predicted = o.PredictedProbability / 100.0;
            var actual = o.ActualCorrect ? 1.0 : 0.0;
            var error = predicted - actual;
            return error * error;
        });

        return sumSquaredError / observations.Count;
    }

    public static double ExpectedCalibrationError(IReadOnlyList<CalibrationBucket> buckets)
    {
        var total = buckets.Sum(b => b.Count);
        if (total == 0)
        {
            throw new ArgumentException("Cannot compute Expected Calibration Error over zero total observations.", nameof(buckets));
        }

        return buckets
            .Where(b => b.Count > 0)
            .Sum(b => (b.Count / (double)total) * Math.Abs((b.MeanPredictedProbability / 100.0) - b.ObservedCorrectRate));
    }

    // Whether the sample size actually supports a fine-grained (e.g. 10-bucket) ECE, rather
    // than computing one anyway because the formula runs on any number of inputs -- the
    // milestone's own required limitations analysis, made deterministic and testable. All
    // non-empty buckets must meet the threshold; an all-empty bucket set never supports it.
    public static bool BucketsSupportFineGrainedEce(
        IReadOnlyList<CalibrationBucket> buckets, int minObservationsPerNonEmptyBucket = 30)
    {
        var nonEmpty = buckets.Where(b => b.Count > 0).ToList();
        return nonEmpty.Count > 0 && nonEmpty.All(b => b.Count >= minObservationsPerNonEmptyBucket);
    }

    // Mirrors Evaluation/CategorySummary.cs's established pattern: group by category in
    // first-seen order (the order scenarios actually ran in), not alphabetized. Milestone 9
    // review finding: Category was captured on every observation but never surfaced anywhere --
    // this is what actually reads it, the same "captured but never consumed" mistake this
    // project has already flagged twice before (Milestone 8's BranchGroup, Milestone 8.5's
    // header comment on SourceCoverageFindings).
    public static IReadOnlyList<CategoryCalibrationSummary> SummarizeByCategory(
        IReadOnlyList<CalibrationObservation> observations)
    {
        var order = new List<string>();
        var grouped = new Dictionary<string, List<CalibrationObservation>>();

        foreach (var observation in observations)
        {
            if (!grouped.TryGetValue(observation.Category, out var items))
            {
                items = new List<CalibrationObservation>();
                grouped[observation.Category] = items;
                order.Add(observation.Category);
            }

            items.Add(observation);
        }

        return order.Select(category =>
        {
            var items = grouped[category];
            var meanPredicted = items.Average(o => o.PredictedProbability);
            var observedRate = items.Count(o => o.ActualCorrect) / (double)items.Count;
            return new CategoryCalibrationSummary(category, items.Count, meanPredicted, observedRate);
        }).ToList();
    }

    // Same "captured but never consumed" gap for Criteria: this is what actually reads it. Only
    // incorrect observations contribute -- a passing criterion on a wrong prediction isn't the
    // story; a failing one is. Aggregated by criterion name in first-seen order, matching
    // SummarizeByCategory/CategorySummary's convention.
    public static IReadOnlyList<CriterionFailureCount> SummarizeCriterionFailures(
        IReadOnlyList<CalibrationObservation> observations)
    {
        var order = new List<string>();
        var counts = new Dictionary<string, int>();

        foreach (var observation in observations.Where(o => !o.ActualCorrect))
        {
            foreach (var criterion in observation.Criteria.Where(c => c.Result == CriterionResult.Fail))
            {
                if (!counts.ContainsKey(criterion.Name))
                {
                    order.Add(criterion.Name);
                    counts[criterion.Name] = 0;
                }

                counts[criterion.Name]++;
            }
        }

        return order.Select(name => new CriterionFailureCount(name, counts[name])).ToList();
    }
}
