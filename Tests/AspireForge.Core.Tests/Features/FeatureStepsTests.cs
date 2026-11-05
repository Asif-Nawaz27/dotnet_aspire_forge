using AspireForge.Core.Features;

namespace AspireForge.Core.Tests.Features;

public class FeatureStepsTests
{
    [Fact]
    public void IsAlreadyInstalled_RecognisesTheMarker()
    {
        Assert.True(FeatureSteps.IsAlreadyInstalled([FeatureSteps.AlreadyInstalled("nothing to change")]));
    }

    [Theory]
    [InlineData("Added ServiceDefaults project")]
    [InlineData("already configured somewhere")]
    public void IsAlreadyInstalled_IsFalse_ForNormalSteps(string step)
    {
        Assert.False(FeatureSteps.IsAlreadyInstalled([step]));
    }

    [Fact]
    public void IsAlreadyInstalled_IsFalse_WhenOtherStepsRan()
    {
        Assert.False(FeatureSteps.IsAlreadyInstalled([FeatureSteps.AlreadyInstalled("x"), "Added Dockerfile"]));
    }
}
