using AspireForge.Analyzers.Rules.Architecture;
using AspireForge.Analyzers.Rules.Observability;
using AspireForge.Analyzers.Rules.Reliability;
using AspireForge.Analyzers.Rules.Security;
using AspireForge.Analyzers.Rules.Testing;
using AspireForge.Core.Analysis;

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
}
