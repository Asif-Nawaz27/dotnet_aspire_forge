using AspireForge.Cli.Output;
using AspireForge.Core.Analysis;

namespace AspireForge.Cli.Tests.Output;

public class SarifReportBuilderTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"aspireforge-sarif-{Guid.NewGuid():N}.json");

    public SarifReportBuilderTests() => File.WriteAllLines(_file, ["{", "  \"a\": 1,", "  \"b\": 2,", "}"]);

    [Fact]
    public void Region_PointsAtTheIssueLine_WhenTheRuleReportsOne()
    {
        var region = BuildRegion(new AnalysisIssue("SEC004", "Secrets", "desc", Severity.Error, _file, LineNumber: 3));

        Assert.Equal(new SarifRegion(3, 3), region);
    }

    [Fact]
    public void Region_SpansTheWholeFile_WhenTheIssueHasNoLine()
    {
        var region = BuildRegion(new AnalysisIssue("SEC003", "HTTPS", "desc", Severity.Warning, _file));

        Assert.Equal(new SarifRegion(1, 4), region);
    }

    public void Dispose()
    {
        File.Delete(_file);
        GC.SuppressFinalize(this);
    }

    private static SarifRegion? BuildRegion(AnalysisIssue issue)
    {
        var log = SarifReportBuilder.Build(new AnalysisResult("App", [issue]), []);
        return Assert.Single(Assert.Single(log.Runs).Results[0].Locations).PhysicalLocation.Region;
    }
}
