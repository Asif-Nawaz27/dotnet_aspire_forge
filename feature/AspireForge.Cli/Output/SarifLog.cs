using System.Text.Json.Serialization;

namespace AspireForge.Cli.Output;

public sealed record SarifLog(
    [property: JsonPropertyName("$schema")] string Schema,
    string Version,
    IReadOnlyList<SarifRun> Runs);

public sealed record SarifRun(SarifTool Tool, IReadOnlyList<SarifResult> Results);

public sealed record SarifTool(SarifToolDriver Driver);

public sealed record SarifToolDriver(string Name, string Version, IReadOnlyList<SarifReportingDescriptor> Rules);

public sealed record SarifReportingDescriptor(string Id, string Name, SarifMessage ShortDescription);

public sealed record SarifResult(
    string RuleId, string Level, SarifMessage Message, IReadOnlyList<SarifLocation> Locations);

public sealed record SarifLocation(SarifPhysicalLocation PhysicalLocation);

public sealed record SarifPhysicalLocation(SarifArtifactLocation ArtifactLocation, SarifRegion? Region = null);

public sealed record SarifArtifactLocation(string Uri);

public sealed record SarifRegion(int StartLine, int EndLine);

public sealed record SarifMessage(string Text);
