namespace PokeJudge.Tests.AI;

using PokeJudge.AI;

public class GeminiEmbeddingResponseParserTests
{
    [Fact]
    public void Parse_MatchingCount_ReturnsVectorsInOrder()
    {
        const string json = """
            {
              "embeddings": [
                { "values": [0.1, 0.2, 0.3] },
                { "values": [0.4, 0.5, 0.6] }
              ]
            }
            """;

        var result = GeminiEmbeddingResponseParser.Parse(json, expectedCount: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(new float[] { 0.1f, 0.2f, 0.3f }, result[0]);
        Assert.Equal(new float[] { 0.4f, 0.5f, 0.6f }, result[1]);
    }

    [Fact]
    public void Parse_FewerEmbeddingsThanRequested_ThrowsNamingBothCounts()
    {
        const string json = """
            {
              "embeddings": [
                { "values": [0.1, 0.2, 0.3] }
              ]
            }
            """;

        var ex = Assert.Throws<InvalidOperationException>(
            () => GeminiEmbeddingResponseParser.Parse(json, expectedCount: 3));

        Assert.Contains("1", ex.Message);
        Assert.Contains("3", ex.Message);
    }

    [Fact]
    public void Parse_MoreEmbeddingsThanRequested_ThrowsNamingBothCounts()
    {
        const string json = """
            {
              "embeddings": [
                { "values": [0.1] },
                { "values": [0.2] }
              ]
            }
            """;

        var ex = Assert.Throws<InvalidOperationException>(
            () => GeminiEmbeddingResponseParser.Parse(json, expectedCount: 1));

        Assert.Contains("2", ex.Message);
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void Parse_MissingEmbeddingsField_ThrowsRatherThanReturningEmptyResult()
    {
        const string json = """{ "someOtherField": true }""";

        Assert.Throws<InvalidOperationException>(
            () => GeminiEmbeddingResponseParser.Parse(json, expectedCount: 1));
    }

    [Fact]
    public void Parse_EmptyEmbeddingsArrayWithZeroExpected_ReturnsEmptyList()
    {
        const string json = """{ "embeddings": [] }""";

        var result = GeminiEmbeddingResponseParser.Parse(json, expectedCount: 0);

        Assert.Empty(result);
    }
}
