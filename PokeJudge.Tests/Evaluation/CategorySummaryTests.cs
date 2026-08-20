namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Evaluation;

public class CategorySummaryTests
{
    [Fact]
    public void Summarize_NoResults_ReturnsEmpty()
    {
        var summary = CategorySummary.Summarize(Array.Empty<(string Category, bool Passed)>());

        Assert.Empty(summary);
    }

    [Fact]
    public void Summarize_SingleCategoryAllPassed_ReturnsFullyPassedCount()
    {
        var summary = CategorySummary.Summarize(new[]
        {
            ("Prize Errors", true),
            ("Prize Errors", true),
        });

        var outcome = Assert.Single(summary);
        Assert.Equal("Prize Errors", outcome.Category);
        Assert.Equal(2, outcome.Passed);
        Assert.Equal(2, outcome.Total);
    }

    [Fact]
    public void Summarize_SingleCategoryMixedResults_CountsPassedAndTotalSeparately()
    {
        var summary = CategorySummary.Summarize(new[]
        {
            ("Gameplay Error", true),
            ("Gameplay Error", false),
            ("Gameplay Error", true),
        });

        var outcome = Assert.Single(summary);
        Assert.Equal(2, outcome.Passed);
        Assert.Equal(3, outcome.Total);
    }

    [Fact]
    public void Summarize_MultipleCategories_GroupsEachIndependently()
    {
        var summary = CategorySummary.Summarize(new[]
        {
            ("Tournament Procedure", true),
            ("Prize Errors", false),
            ("Tournament Procedure", false),
        });

        Assert.Equal(2, summary.Count);
        Assert.Contains(summary, o => o.Category == "Tournament Procedure" && o.Passed == 1 && o.Total == 2);
        Assert.Contains(summary, o => o.Category == "Prize Errors" && o.Passed == 0 && o.Total == 1);
    }

    [Fact]
    public void Summarize_InterleavedCategories_PreservesFirstSeenOrder()
    {
        var summary = CategorySummary.Summarize(new[]
        {
            ("B", true),
            ("A", true),
            ("B", false),
            ("A", false),
        });

        Assert.Equal(new[] { "B", "A" }, summary.Select(o => o.Category));
    }
}
