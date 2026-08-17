namespace PokeJudge.Tests.Grounding;

using PokeJudge.Grounding;
using PokeJudge.StructuredState;

public class SourceSupportAssignerTests
{
    private static readonly GroundingAssessment AllExplicitNoConflict = new(
        new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) },
        ConflictDetected: false,
        Rationale: "Directly addressed.");

    [Fact]
    public void Assign_RetrievalWasEmpty_ReturnsInsufficientRegardlessOfAssessment()
    {
        var (result, _) = SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: false,
            allCitationsExist: true,
            factsWereSufficient: true,
            assessment: AllExplicitNoConflict);

        Assert.Equal(SourceSupport.Insufficient, result);
    }

    [Fact]
    public void Assign_CitationsDidNotAllExist_ReturnsInsufficient()
    {
        var (result, _) = SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: true,
            allCitationsExist: false,
            factsWereSufficient: true,
            assessment: AllExplicitNoConflict);

        Assert.Equal(SourceSupport.Insufficient, result);
    }

    [Fact]
    public void Assign_FactsWereNotSufficient_ReturnsInsufficient()
    {
        var (result, _) = SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: true,
            allCitationsExist: true,
            factsWereSufficient: false,
            assessment: AllExplicitNoConflict);

        Assert.Equal(SourceSupport.Insufficient, result);
    }

    [Fact]
    public void Assign_ACitationIsUnsupported_ReturnsInsufficient()
    {
        var assessment = new GroundingAssessment(
            new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.Unsupported) },
            ConflictDetected: false,
            Rationale: "Does not actually say that.");

        var (result, _) = SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: true,
            allCitationsExist: true,
            factsWereSufficient: true,
            assessment: assessment);

        Assert.Equal(SourceSupport.Insufficient, result);
    }

    [Fact]
    public void Assign_ACitedIdIsMissingFromTheAssessment_TreatsItAsUnsupported()
    {
        // The model was asked to classify every cited chunk ID; if it drops one from
        // its response, that must not be treated as favorably as ExplicitSupport.
        var assessment = new GroundingAssessment(
            new List<CitationGroundingCheck>(),
            ConflictDetected: false,
            Rationale: "n/a");

        var (result, _) = SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: true,
            allCitationsExist: true,
            factsWereSufficient: true,
            assessment: assessment);

        Assert.Equal(SourceSupport.Insufficient, result);
    }

    [Fact]
    public void Assign_ACitationIsInterpretationOnly_ReturnsPartial()
    {
        var assessment = new GroundingAssessment(
            new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.Interpretation) },
            ConflictDetected: false,
            Rationale: "Requires judge discretion.");

        var (result, _) = SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: true,
            allCitationsExist: true,
            factsWereSufficient: true,
            assessment: assessment);

        Assert.Equal(SourceSupport.Partial, result);
    }

    [Fact]
    public void Assign_ConflictDetectedEvenWithAllExplicitSupport_ReturnsPartial()
    {
        var assessment = AllExplicitNoConflict with { ConflictDetected = true };

        var (result, _) = SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: true,
            allCitationsExist: true,
            factsWereSufficient: true,
            assessment: assessment);

        Assert.Equal(SourceSupport.Partial, result);
    }

    [Fact]
    public void Assign_AllExplicitSupportNoConflictAllDeterministicChecksPass_ReturnsStrong()
    {
        var (result, _) = SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: true,
            allCitationsExist: true,
            factsWereSufficient: true,
            assessment: AllExplicitNoConflict);

        Assert.Equal(SourceSupport.Strong, result);
    }

    [Fact]
    public void Assign_ModelClassifiesTheSameChunkIdTwice_ThrowsNamingTheDuplicate()
    {
        // The model was asked to classify each cited passage once. Two entries for the
        // same chunk ID (even if they agree) is a malformed response that must fail
        // loudly rather than be silently resolved by picking one.
        var assessment = new GroundingAssessment(
            new List<CitationGroundingCheck>
            {
                new("A1#0", CitationSupportLevel.ExplicitSupport),
                new("A1#0", CitationSupportLevel.Unsupported),
            },
            ConflictDetected: false,
            Rationale: "n/a");

        var ex = Assert.Throws<InvalidOperationException>(() => SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: true,
            allCitationsExist: true,
            factsWereSufficient: true,
            assessment: assessment));

        Assert.Contains("A1#0", ex.Message);
    }

    [Fact]
    public void Assign_AlwaysReturnsANonEmptyRationale()
    {
        var (_, rationale) = SourceSupportAssigner.Assign(
            citedChunkIds: new[] { "A1#0" },
            retrievalNonEmpty: true,
            allCitationsExist: true,
            factsWereSufficient: true,
            assessment: AllExplicitNoConflict);

        Assert.False(string.IsNullOrWhiteSpace(rationale));
    }
}
