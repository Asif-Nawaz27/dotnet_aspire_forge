using AspireForge.Cli.Commands;
using AspireForge.Generators.GitHub;

namespace AspireForge.Cli.Tests.Commands;

public class ToolVersionTests
{
    // Generated workflows install this version. If the generator's version ever differs from the tool
    // package's, every generated project's CI asks NuGet for a release that doesn't exist.
    [Fact]
    public void Generator_ReportsTheVersionOfTheToolPackage() =>
        Assert.Equal(ToolVersion.Read(typeof(DoctorCommand).Assembly), ToolVersion.Current);

    [Fact]
    public void Version_HasNoCommitHash() =>
        Assert.DoesNotContain("+", ToolVersion.Current);
}
