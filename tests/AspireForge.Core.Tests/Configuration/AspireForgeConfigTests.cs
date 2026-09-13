using AspireForge.Core.Configuration;

namespace AspireForge.Core.Tests.Configuration;

public class AspireForgeConfigTests
{
    [Fact]
    public void Rules_DefaultsToEmpty_SoLookupsDoNotThrow()
    {
        var config = new AspireForgeConfig();

        Assert.False(config.Rules.TryGetValue("SEC001", out _));
    }

    [Fact]
    public void RuleConfig_EnabledAndSeverity_DefaultToNull()
    {
        var ruleConfig = new RuleConfig();

        Assert.Null(ruleConfig.Enabled);
        Assert.Null(ruleConfig.Severity);
    }
}
