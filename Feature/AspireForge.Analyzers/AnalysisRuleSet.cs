using AspireForge.Analyzers.Rules.Architecture;
using AspireForge.Analyzers.Rules.Deployment;
using AspireForge.Analyzers.Rules.Observability;
using AspireForge.Analyzers.Rules.Performance;
using AspireForge.Analyzers.Rules.Reliability;
using AspireForge.Analyzers.Rules.Security;
using AspireForge.Analyzers.Rules.Testing;
using AspireForge.Analyzers.Vulnerabilities;
using AspireForge.Core.Analysis;
using AspireForge.Core.Configuration;

namespace AspireForge.Analyzers;

public static class AnalysisRuleSet
{
    // DEP002 (vulnerable packages) runs the dotnet CLI and queries NuGet over the network, so it's only
    // included when asked for - `aspireforge doctor --vulnerable`.
    public static IReadOnlyList<IAnalysisRule> CreateDefault(bool includeVulnerabilityScan = false) =>
        includeVulnerabilityScan
            ? [.. StaticRules(), new DEP002VulnerablePackages(new DotnetListPackageScanner())]
            : StaticRules();

    private static IReadOnlyList<IAnalysisRule> StaticRules() =>
    [
        new SEC001AuthenticationNotConfigured(),
        new SEC002CorsPolicyAllowsUnrestrictedOrigins(),
        new SEC003HttpsRedirectionMissing(),
        new SEC004SecretsInConfiguration(),
        new SEC005WeakJwtValidation(),
        new SEC007DevelopmentFeaturesInProduction(),
        new SEC008AuthorizationNotEnforced(),
        new SEC009MiddlewareOrder(),
        new SEC010TlsCertificateValidationDisabled(),
        new SEC011SqlInjectionInRawSql(),
        new SEC012UnsafeDeserialization(),
        new SEC013HardcodedSecretsInCode(),
        new REL001HealthChecksMissing(),
        new REL002GlobalExceptionHandlingMissing(),
        new REL003OutboundHttpWithoutResilience(),
        new REL004HealthChecksIgnoreDependencies(),
        new REL005EnsureCreatedUsed(),
        new REL006DatabaseRetryPolicyMissing(),
        new REL007SettingsNotValidatedAtStartup(),
        new REL008AsyncVoidMethod(),
        new OBS001OpenTelemetryMissing(),
        new OBS002StructuredLoggingMissing(),
        new OBS003TelemetryExporterMissing(),
        new OBS004SensitiveDataLoggingEnabled(),
        new OBS005ConsoleOutputInsteadOfLogger(),
        new OBS006InterpolatedLogMessage(),
        new OBS007VerboseLoggingByDefault(),
        new TEST001NoTestProjectDetected(),
        new TEST002NoIntegrationTestsDetected(),
        new ARCH001ApiDirectlyAccessesPersistenceLayer(),
        new ARCH002LayerDependencyReversed(),
        new PERF001ResponseCompressionMissing(),
        new PERF002RateLimitingMissing(),
        new PERF003BlockingCallOnAsyncCode(),
        new PERF004SynchronousIoAllowed(),
        new DEP001UnsupportedTargetFramework(),
        new DEP003ContainerProblems(),
        new DEP004PrereleasePackages(),
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
