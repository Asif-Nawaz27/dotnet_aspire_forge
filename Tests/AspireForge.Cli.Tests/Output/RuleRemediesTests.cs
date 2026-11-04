using AspireForge.Cli.Output;

namespace AspireForge.Cli.Tests.Output;

public class RuleRemediesTests
{
    private static readonly IReadOnlySet<string> Fixable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "REL001", "SEC003" };

    [Theory]
    [InlineData("REL001", "aspireforge fix REL001")]
    [InlineData("sec003", "aspireforge fix sec003")]
    [InlineData("SEC001", "aspireforge add authentication")]
    [InlineData("OBS001", "aspireforge add telemetry")]
    [InlineData("OBS002", "aspireforge add telemetry")]
    public void CommandFor_ReturnsTheOneCommandThatResolvesTheFinding(string ruleId, string command) =>
        Assert.Equal(command, RuleRemedies.CommandFor(ruleId, Fixable));

    [Theory]
    [InlineData("SEC013")]
    [InlineData("ARCH001")]
    public void CommandFor_ReturnsNothing_WhenTheFixIsACodeChange(string ruleId) =>
        Assert.Null(RuleRemedies.CommandFor(ruleId, Fixable));
}
