namespace PokeJudge.Tests.AI;

using PokeJudge.AI;

public class JevSettingsTests
{
    private static Func<string, string?> Config(params (string Key, string Value)[] values) =>
        key => values.FirstOrDefault(v => v.Key == key).Value;

    [Fact]
    public void Read_OnlyApiKey_UsesDefaults()
    {
        var (settings, error) = JevSettings.Read(Config(("Jev:ApiKey", " key ")));

        Assert.Null(error);
        Assert.Equal(new JevSettings("key", "jev-latest", 100, 4), settings);
    }

    [Fact]
    public void Read_OverridesModelCandidateCountAndMaxPerSection()
    {
        var (settings, _) = JevSettings.Read(Config(
            ("Jev:ApiKey", "key"), ("Jev:Model", "jev-1.13.0"), ("Jev:CandidateCount", "20"), ("Jev:MaxPerSection", "2")));

        Assert.Equal(new JevSettings("key", "jev-1.13.0", 20, 2), settings);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    public void Read_InvalidMaxPerSection_ReturnsError(string value)
    {
        var (_, error) = JevSettings.Read(Config(("Jev:ApiKey", "key"), ("Jev:MaxPerSection", value)));

        Assert.Equal($"Jev:MaxPerSection must be a positive integer, got \"{value}\".", error);
    }

    [Fact]
    public void Read_MissingApiKey_ReturnsErrorWithTheSetupCommand()
    {
        var (settings, error) = JevSettings.Read(Config());

        Assert.Null(settings);
        Assert.Contains("dotnet user-secrets set \"Jev:ApiKey\"", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    public void Read_InvalidCandidateCount_ReturnsError(string value)
    {
        var (_, error) = JevSettings.Read(Config(("Jev:ApiKey", "key"), ("Jev:CandidateCount", value)));

        Assert.Equal($"Jev:CandidateCount must be a positive integer, got \"{value}\".", error);
    }
}
