namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Retrieval;

public class RetrievalQueryBuilderTests
{
    [Fact]
    public void Build_NoConfirmedFactsYet_ReturnsJustTheScenarioDescription()
    {
        var query = RetrievalQueryBuilder.Build("A player forgot to take a Prize card.", new List<string>());

        Assert.Equal("A player forgot to take a Prize card.", query);
    }

    [Fact]
    public void Build_WithConfirmedFacts_AppendsEachFactToTheScenarioDescription()
    {
        var query = RetrievalQueryBuilder.Build(
            "A player forgot to take a Prize card.",
            new List<string> { "A Pokemon was Knocked Out.", "The error was discovered two turns later." });

        Assert.Contains("A player forgot to take a Prize card.", query);
        Assert.Contains("A Pokemon was Knocked Out.", query);
        Assert.Contains("The error was discovered two turns later.", query);
    }

    [Fact]
    public void Build_QueryGrowsAsMoreFactsAreConfirmed()
    {
        var scenario = "A player forgot to take a Prize card.";

        var afterOneFact = RetrievalQueryBuilder.Build(scenario, new List<string> { "Fact one." });
        var afterTwoFacts = RetrievalQueryBuilder.Build(scenario, new List<string> { "Fact one.", "Fact two." });

        Assert.True(afterTwoFacts.Length > afterOneFact.Length);
        Assert.Contains("Fact two.", afterTwoFacts);
        Assert.DoesNotContain("Fact two.", afterOneFact);
    }
}
