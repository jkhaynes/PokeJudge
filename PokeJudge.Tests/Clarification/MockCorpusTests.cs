namespace PokeJudge.Tests.Clarification;

using PokeJudge.Clarification;

public class MockCorpusTests
{
    [Fact]
    public void HasExactlyThreeScenarios()
    {
        Assert.Equal(3, MockCorpus.Scenarios.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EachScenario_Has2To4SnippetsWithUniqueStableIds(int index)
    {
        var scenario = MockCorpus.Scenarios[index];

        Assert.InRange(scenario.Snippets.Count, 2, 4);
        Assert.Equal(scenario.Snippets.Count, scenario.Snippets.Select(s => s.Id).Distinct().Count());
        Assert.All(scenario.Snippets, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            Assert.False(string.IsNullOrWhiteSpace(s.Text));
        });
    }

    [Fact]
    public void EachScenario_HasNonEmptyTitleAndDescription()
    {
        Assert.All(MockCorpus.Scenarios, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Title));
            Assert.False(string.IsNullOrWhiteSpace(s.Description));
        });
    }
}
