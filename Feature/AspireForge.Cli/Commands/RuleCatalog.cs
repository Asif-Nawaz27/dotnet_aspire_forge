namespace AspireForge.Cli.Commands;

public sealed record RuleCatalogEntry(string Category, string RuleId, string ShortName, string PassedMessage);

public static class RuleCatalog
{
    public static readonly IReadOnlyList<RuleCatalogEntry> Entries =
    [
        new("Security", "SEC001", "Authentication", "Authentication configured"),
        new("Security", "SEC002", "CORS", "CORS policy restricts origins"),
        new("Security", "SEC003", "HTTPS", "HTTPS configured"),
        new("Security", "SEC004", "Secrets in config", "No secrets in configuration files"),
        new("Security", "SEC005", "JWT validation", "JWT validation not weakened"),
        new("Security", "SEC008", "Authorization", "Authorization enforced on endpoints"),
        new("Security", "SEC009", "Middleware order", "Middleware in a working order"),
        new("Security", "SEC010", "TLS validation", "TLS certificates validated"),
        new("Security", "SEC011", "SQL injection", "No SQL built from runtime values"),
        new("Security", "SEC012", "Deserialization", "No type-trusting deserialization"),
        new("Security", "SEC013", "Secrets in code", "No hard-coded secrets in code"),
        new("Reliability", "REL001", "Health checks", "Health checks configured"),
        new("Reliability", "REL002", "Exception handling", "Global exception handling"),
        new("Reliability", "REL003", "HTTP resilience", "Outbound HTTP has resilience"),
        new("Reliability", "REL004", "Dependency health checks", "Health checks cover dependencies"),
        new("Reliability", "REL005", "Migrations", "No EnsureCreated in app code"),
        new("Reliability", "REL006", "DB retry policy", "Database retry policy configured"),
        new("Reliability", "REL007", "Settings validation", "Settings validated at startup"),
        new("Reliability", "REL008", "async void", "No async void methods"),
        new("Observability", "OBS001", "OpenTelemetry", "OpenTelemetry configured"),
        new("Observability", "OBS002", "Structured logging", "Structured logging"),
        new("Observability", "OBS003", "Telemetry exporter", "Telemetry exporter configured"),
        new("Observability", "OBS004", "Sensitive data logging", "Sensitive data logging limited to development"),
        new("Observability", "OBS005", "Console output", "No Console output in application code"),
        new("Observability", "OBS006", "Log templates", "Log messages use templates"),
        new("Observability", "OBS007", "Log level", "Production log level not verbose"),
        new("Testing", "TEST001", "Unit tests", "Unit tests detected"),
        new("Testing", "TEST002", "Integration tests", "Integration tests detected"),
        new("Architecture", "ARCH001", "Dependency violations", "No obvious dependency violations"),
        new("Architecture", "ARCH002", "Layer direction", "Layer dependencies point inward"),
        new("Performance", "PERF001", "Response compression", "Response compression configured"),
        new("Performance", "PERF002", "Rate limiting", "Rate limiting configured"),
        new("Performance", "PERF003", "Blocking calls", "No blocking calls on async code"),
        new("Performance", "PERF004", "Synchronous I/O", "Synchronous I/O disabled"),
        new("Deployment", "DEP001", ".NET version", "Supported .NET version"),
        new("Deployment", "DEP003", "Container image", "Container image follows good practice"),
        new("Deployment", "DEP004", "Package versions", "Packages pinned to stable releases"),
    ];
}
