namespace PokeJudge.Tests.Reliability;

using PokeJudge.Evaluation;
using PokeJudge.Grounding;
using PokeJudge.Reliability;
using PokeJudge.StructuredState;

public class CalibrationAnalysisTests
{
    private static CalibrationObservation Observation(
        string scenarioId, int predictedProbability, bool actualCorrect, string category = "Prize Errors",
        IReadOnlyList<CriterionOutcome>? criteria = null, SourceSupport validatedSourceSupport = SourceSupport.Strong,
        bool allCitationsExist = true, bool conflictDetected = false, int explicitSupportCitationCount = 1,
        int interpretationCitationCount = 0, int unsupportedCitationCount = 0) =>
        new(scenarioId, category, predictedProbability, actualCorrect, criteria ?? new List<CriterionOutcome>(),
            validatedSourceSupport, allCitationsExist, conflictDetected, explicitSupportCitationCount,
            interpretationCitationCount, unsupportedCitationCount);

    private static CriterionOutcome Criterion(string name, CriterionResult result) => new(name, result, "detail");

    // --- Bucket ---

    [Fact]
    public void Bucket_EmptyObservations_ReturnsEmptyBucketsCoveringFullRange()
    {
        var buckets = CalibrationAnalysis.Bucket(Array.Empty<CalibrationObservation>(), bucketCount: 10);

        Assert.Equal(10, buckets.Count);
        Assert.All(buckets, b => Assert.Equal(0, b.Count));
        Assert.Equal(0, buckets[0].LowerBound);
        Assert.Equal(100, buckets[9].UpperBound);
    }

    [Fact]
    public void Bucket_ProbabilityOfZero_LandsInFirstBucket()
    {
        var buckets = CalibrationAnalysis.Bucket(new[] { Observation("a", 0, true) }, bucketCount: 10);

        Assert.Equal(1, buckets[0].Count);
        Assert.All(buckets.Skip(1), b => Assert.Equal(0, b.Count));
    }

    [Fact]
    public void Bucket_ProbabilityOfExactly100_LandsInLastBucketNotOutOfRange()
    {
        var buckets = CalibrationAnalysis.Bucket(new[] { Observation("a", 100, true) }, bucketCount: 10);

        Assert.Equal(1, buckets[9].Count);
        Assert.All(buckets.Take(9), b => Assert.Equal(0, b.Count));
    }

    [Fact]
    public void Bucket_ObservationsSpanMultipleBuckets_DistributedCorrectly()
    {
        var observations = new[]
        {
            Observation("a", 5, true),
            Observation("b", 15, true),
            Observation("c", 95, false),
        };

        var buckets = CalibrationAnalysis.Bucket(observations, bucketCount: 10);

        Assert.Equal(1, buckets[0].Count);
        Assert.Equal(1, buckets[1].Count);
        Assert.Equal(1, buckets[9].Count);
    }

    [Fact]
    public void Bucket_ObservedCorrectRate_ComputedPerBucket()
    {
        var observations = new[]
        {
            Observation("a", 85, true),
            Observation("b", 88, true),
            Observation("c", 82, false),
        };

        var buckets = CalibrationAnalysis.Bucket(observations, bucketCount: 10);

        var populated = buckets.Single(b => b.Count == 3);
        Assert.Equal(2.0 / 3.0, populated.ObservedCorrectRate, precision: 10);
    }

    [Fact]
    public void Bucket_MeanPredictedProbability_ComputedPerBucket()
    {
        // Both land in the same [80,90) bucket -- 90 itself would round into the next bucket.
        var observations = new[] { Observation("a", 80, true), Observation("b", 85, false) };

        var buckets = CalibrationAnalysis.Bucket(observations, bucketCount: 10);

        var populated = buckets.Single(b => b.Count == 2);
        Assert.Equal(82.5, populated.MeanPredictedProbability, precision: 10);
    }

    [Fact]
    public void Bucket_EmptyBucket_HasZeroMeanAndZeroObservedRate()
    {
        var buckets = CalibrationAnalysis.Bucket(new[] { Observation("a", 5, true) }, bucketCount: 10);

        var empty = buckets[9];
        Assert.Equal(0, empty.Count);
        Assert.Equal(0.0, empty.MeanPredictedProbability);
        Assert.Equal(0.0, empty.ObservedCorrectRate);
    }

    [Fact]
    public void Bucket_ThreeCoarseBuckets_CoversFullRangeWithoutGaps()
    {
        var buckets = CalibrationAnalysis.Bucket(Array.Empty<CalibrationObservation>(), bucketCount: 3);

        Assert.Equal(3, buckets.Count);
        Assert.Equal(0, buckets[0].LowerBound);
        Assert.Equal(buckets[0].UpperBound, buckets[1].LowerBound);
        Assert.Equal(buckets[1].UpperBound, buckets[2].LowerBound);
        Assert.Equal(100, buckets[2].UpperBound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Bucket_NonPositiveBucketCount_ThrowsArgumentOutOfRangeException(int bucketCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CalibrationAnalysis.Bucket(Array.Empty<CalibrationObservation>(), bucketCount));
    }

    [Fact]
    public void Bucket_OutOfRangeProbability_ClampedRatherThanThrowing()
    {
        // Defensive normalization, not full validation -- the model is schema-instructed to
        // always return 0-100, but one malformed value shouldn't discard an entire live run's
        // worth of other, valid observations.
        var buckets = CalibrationAnalysis.Bucket(new[] { Observation("a", 150, true) }, bucketCount: 10);

        Assert.Equal(1, buckets[9].Count);
    }

    // --- BrierScore ---

    [Fact]
    public void BrierScore_PerfectPredictions_ReturnsZero()
    {
        var observations = new[] { Observation("a", 100, true), Observation("b", 0, false) };

        var score = CalibrationAnalysis.BrierScore(observations);

        Assert.Equal(0.0, score, precision: 10);
    }

    [Fact]
    public void BrierScore_WorstPossiblePredictions_ReturnsOne()
    {
        var observations = new[] { Observation("a", 100, false), Observation("b", 0, true) };

        var score = CalibrationAnalysis.BrierScore(observations);

        Assert.Equal(1.0, score, precision: 10);
    }

    [Fact]
    public void BrierScore_FiftyPercentPredictions_ReturnsQuarter()
    {
        var observations = new[] { Observation("a", 50, true) };

        var score = CalibrationAnalysis.BrierScore(observations);

        Assert.Equal(0.25, score, precision: 10);
    }

    [Fact]
    public void BrierScore_EmptyObservations_Throws()
    {
        Assert.Throws<ArgumentException>(() => CalibrationAnalysis.BrierScore(Array.Empty<CalibrationObservation>()));
    }

    // --- ExpectedCalibrationError ---

    [Fact]
    public void ExpectedCalibrationError_PerfectCalibration_ReturnsZero()
    {
        // Bucket's mean predicted probability exactly matches its observed correct rate:
        // predicted 75%, and 3 of 4 (75%) actually correct.
        var observations = new[]
        {
            Observation("a", 75, true),
            Observation("b", 75, true),
            Observation("c", 75, true),
            Observation("d", 75, false),
        };
        var buckets = CalibrationAnalysis.Bucket(observations, bucketCount: 10);

        var ece = CalibrationAnalysis.ExpectedCalibrationError(buckets);

        Assert.Equal(0.0, ece, precision: 10);
    }

    [Fact]
    public void ExpectedCalibrationError_TotallyMiscalibrated_ReturnsExpectedWeightedError()
    {
        // Predicted 100% but actually never correct -> full miscalibration for this bucket.
        var observations = new[] { Observation("a", 95, false), Observation("b", 95, false) };
        var buckets = CalibrationAnalysis.Bucket(observations, bucketCount: 10);

        var ece = CalibrationAnalysis.ExpectedCalibrationError(buckets);

        Assert.Equal(0.95, ece, precision: 10);
    }

    [Fact]
    public void ExpectedCalibrationError_EmptyBuckets_Throws()
    {
        var buckets = CalibrationAnalysis.Bucket(Array.Empty<CalibrationObservation>(), bucketCount: 10);

        Assert.Throws<ArgumentException>(() => CalibrationAnalysis.ExpectedCalibrationError(buckets));
    }

    // --- BucketsSupportFineGrainedEce ---

    [Fact]
    public void BucketsSupportFineGrainedEce_AllNonEmptyBucketsMeetThreshold_ReturnsTrue()
    {
        var observations = Enumerable.Range(0, 30).Select(i => Observation($"s{i}", 85, true)).ToList();
        var buckets = CalibrationAnalysis.Bucket(observations, bucketCount: 10);

        Assert.True(CalibrationAnalysis.BucketsSupportFineGrainedEce(buckets, minObservationsPerNonEmptyBucket: 30));
    }

    [Fact]
    public void BucketsSupportFineGrainedEce_ANonEmptyBucketBelowThreshold_ReturnsFalse()
    {
        var observations = new[] { Observation("a", 85, true), Observation("b", 15, false) };
        var buckets = CalibrationAnalysis.Bucket(observations, bucketCount: 10);

        Assert.False(CalibrationAnalysis.BucketsSupportFineGrainedEce(buckets, minObservationsPerNonEmptyBucket: 30));
    }

    [Fact]
    public void BucketsSupportFineGrainedEce_AllBucketsEmpty_ReturnsFalse()
    {
        var buckets = CalibrationAnalysis.Bucket(Array.Empty<CalibrationObservation>(), bucketCount: 10);

        Assert.False(CalibrationAnalysis.BucketsSupportFineGrainedEce(buckets));
    }

    // --- ExcludeKnownIssues ---

    [Fact]
    public void ExcludeKnownIssues_FiltersMissedPrizeAndMulliganNotTaken()
    {
        var observations = new[]
        {
            Observation("missed-prize", 60, false),
            Observation("mulligan-not-taken", 70, true),
            Observation("gx-attack-twice", 90, true),
        };

        var filtered = CalibrationAnalysis.ExcludeKnownIssues(observations);

        Assert.Single(filtered);
        Assert.Equal("gx-attack-twice", filtered[0].ScenarioId);
    }

    [Fact]
    public void ExcludeKnownIssues_NoKnownIssueScenariosPresent_ReturnsAllUnchanged()
    {
        var observations = new[] { Observation("gx-attack-twice", 90, true), Observation("notes", 85, true) };

        var filtered = CalibrationAnalysis.ExcludeKnownIssues(observations);

        Assert.Equal(2, filtered.Count);
    }

    // --- SummarizeByCategory ---
    // Mirrors CategorySummary's established pattern (Evaluation/CategorySummary.cs): group by
    // category in first-seen order, not alphabetized, so the printed summary reads in the same
    // order scenarios actually ran in.

    [Fact]
    public void SummarizeByCategory_EmptyObservations_ReturnsEmptyList()
    {
        var summary = CalibrationAnalysis.SummarizeByCategory(Array.Empty<CalibrationObservation>());

        Assert.Empty(summary);
    }

    [Fact]
    public void SummarizeByCategory_SingleCategory_ComputesCountMeanAndObservedRate()
    {
        var observations = new[]
        {
            Observation("a", 90, true, category: "Prize Errors"),
            Observation("b", 80, false, category: "Prize Errors"),
        };

        var summary = CalibrationAnalysis.SummarizeByCategory(observations);

        var prizeErrors = Assert.Single(summary);
        Assert.Equal("Prize Errors", prizeErrors.Category);
        Assert.Equal(2, prizeErrors.Count);
        Assert.Equal(85.0, prizeErrors.MeanPredictedProbability, precision: 10);
        Assert.Equal(0.5, prizeErrors.ObservedCorrectRate, precision: 10);
    }

    [Fact]
    public void SummarizeByCategory_MultipleCategories_GroupedSeparatelyInFirstSeenOrder()
    {
        var observations = new[]
        {
            Observation("a", 90, true, category: "Timing Questions"),
            Observation("b", 80, true, category: "Prize Errors"),
            Observation("c", 70, false, category: "Timing Questions"),
        };

        var summary = CalibrationAnalysis.SummarizeByCategory(observations);

        Assert.Equal(2, summary.Count);
        Assert.Equal("Timing Questions", summary[0].Category);
        Assert.Equal(2, summary[0].Count);
        Assert.Equal("Prize Errors", summary[1].Category);
        Assert.Equal(1, summary[1].Count);
    }

    // --- SummarizeCriterionFailures ---

    [Fact]
    public void SummarizeCriterionFailures_EmptyObservations_ReturnsEmptyList()
    {
        var summary = CalibrationAnalysis.SummarizeCriterionFailures(Array.Empty<CalibrationObservation>());

        Assert.Empty(summary);
    }

    [Fact]
    public void SummarizeCriterionFailures_OnlyCorrectObservations_ReturnsEmptyList()
    {
        // Even if a "correct" observation happens to carry a failing criterion (not possible in
        // practice given how ActualCorrect is derived, but the function's contract is explicit:
        // only incorrect observations contribute), it must not be counted.
        var observations = new[]
        {
            Observation("a", 90, true, criteria: new[] { Criterion("Initial retrieval", CriterionResult.Fail) }),
        };

        var summary = CalibrationAnalysis.SummarizeCriterionFailures(observations);

        Assert.Empty(summary);
    }

    [Fact]
    public void SummarizeCriterionFailures_IncorrectObservation_CountsOnlyFailingCriteria()
    {
        var observations = new[]
        {
            Observation("a", 90, false, criteria: new[]
            {
                Criterion("Initial retrieval", CriterionResult.Fail),
                Criterion("Sufficiency timing", CriterionResult.Pass),
            }),
        };

        var summary = CalibrationAnalysis.SummarizeCriterionFailures(observations);

        var failure = Assert.Single(summary);
        Assert.Equal("Initial retrieval", failure.CriterionName);
        Assert.Equal(1, failure.FailureCount);
    }

    [Fact]
    public void SummarizeCriterionFailures_MultipleIncorrectObservationsShareAFailingCriterion_AggregatesCount()
    {
        var observations = new[]
        {
            Observation("a", 90, false, criteria: new[] { Criterion("Initial retrieval", CriterionResult.Fail) }),
            Observation("b", 85, false, criteria: new[] { Criterion("Initial retrieval", CriterionResult.Fail) }),
            Observation("c", 95, false, criteria: new[] { Criterion("Final Source Support", CriterionResult.Fail) }),
        };

        var summary = CalibrationAnalysis.SummarizeCriterionFailures(observations);

        Assert.Equal(2, summary.Count);
        Assert.Equal("Initial retrieval", summary[0].CriterionName);
        Assert.Equal(2, summary[0].FailureCount);
        Assert.Equal("Final Source Support", summary[1].CriterionName);
        Assert.Equal(1, summary[1].FailureCount);
    }
}
