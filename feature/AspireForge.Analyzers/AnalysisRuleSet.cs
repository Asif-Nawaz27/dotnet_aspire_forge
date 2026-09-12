using AspireForge.Analyzers.Rules.Architecture;
using AspireForge.Analyzers.Rules.Observability;
using AspireForge.Analyzers.Rules.Reliability;
using AspireForge.Analyzers.Rules.Security;
using AspireForge.Analyzers.Rules.Testing;
using AspireForge.Core.Analysis;
using AspireForge.Core.Configuration;

namespace AspireForge.Analyzers;

public static class AnalysisRuleSet
{
    public static IReadOnlyList<IAnalysisRule> CreateDefault() =>
    [
        new SEC001AuthenticationNotConfigured(),
        new SEC002CorsPolicyAllowsUnrestrictedOrigins(),
        new SEC003HttpsRedirectionMissing(),
        new REL001HealthChecksMissing(),
        new REL002GlobalExceptionHandlingMissing(),
        new OBS001OpenTelemetryMissing(),
        new OBS002StructuredLoggingMissing(),
        new TEST001NoTestProjectDetected(),
        new TEST002NoIntegrationTestsDetected(),
        new ARCH001ApiDirectlyAccessesPersistenceLayer(),
    ];

    public static IReadOnlyList<IAnalysisRule> ApplyConfig(IReadOnlyList<IAnalysisRule> rules, AspireForgeConfig config) =>
        rules.Where(rule => IsEnabled(rule.Id, config)).ToList();

    public static AnalysisResult ApplySeverityOverrides(AnalysisResult result, AspireForgeConfig config)
    {
        var issues = result.Issues
            .Select(issue => config.Rules.TryGetValue(issue.RuleId, out var ruleConfig) && ruleConfig.Severity is { } severity
                ? issue with { Severity = severity }
                : issue)
            .ToList();

        return result with { Issues = issues };
    }

    private static bool IsEnabled(string ruleId, AspireForgeConfig config) =>
        !config.Rules.TryGetValue(ruleId, out var ruleConfig) || ruleConfig.Enabled != false;
}
