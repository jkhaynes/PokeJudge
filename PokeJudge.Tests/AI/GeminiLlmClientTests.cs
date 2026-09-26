namespace PokeJudge.Tests.AI;

using System.Text.Json;
using PokeJudge.AI;

public class GeminiLlmClientTests
{
    private static JsonElement GenerationConfig()
    {
        using var schema = JsonDocument.Parse("""{ "type": "object" }""");
        var body = GeminiLlmClient.BuildRequestBody("system", "user", schema.RootElement.Clone());
        // Same options JsonContent.Create uses when the client sends the request.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return JsonSerializer.SerializeToElement(body, options).GetProperty("generationConfig");
    }

    [Fact]
    public void BuildRequestBody_SendsFixedSeed()
    {
        Assert.True(GenerationConfig().TryGetProperty("seed", out var seed));
        Assert.Equal(GeminiLlmClient.Seed, seed.GetInt32());
    }

    [Fact]
    public void BuildRequestBody_LeavesTemperatureAtModelDefault()
    {
        Assert.False(GenerationConfig().TryGetProperty("temperature", out _));
    }
}
