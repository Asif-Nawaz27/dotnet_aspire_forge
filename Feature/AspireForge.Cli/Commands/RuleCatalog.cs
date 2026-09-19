namespace AspireForge.Cli.Commands;

public sealed record RuleCatalogEntry(string Category, string RuleId, string ShortName, string PassedMessage);

public static class RuleCatalog
{
    public static readonly IReadOnlyList<RuleCatalogEntry> Entries =
    [
        new("Security", "SEC001", "Authentication", "Authentication configured"),
        new("Security", "SEC002", "CORS", "CORS policy restricts origins"),
        new("Security", "SEC003", "HTTPS", "HTTPS configured"),
        new("Reliability", "REL001", "Health checks", "Health checks configured"),
        new("Reliability", "REL002", "Exception handling", "Global exception handling"),
        new("Observability", "OBS001", "OpenTelemetry", "OpenTelemetry configured"),
        new("Observability", "OBS002", "Structured logging", "Structured logging"),
        new("Testing", "TEST001", "Unit tests", "Unit tests detected"),
        new("Testing", "TEST002", "Integration tests", "Integration tests detected"),
        new("Architecture", "ARCH001", "Dependency violations", "No obvious dependency violations"),
    ];
}
