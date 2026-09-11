using AspireForge.Core.Features;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Observability;

public sealed class TelemetryFeatureInstaller : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = "telemetry",
        Name = "OpenTelemetry",
        Description = "Adds OpenTelemetry tracing and metrics with ASP.NET Core instrumentation and a console exporter.",
    };

    public Task<IReadOnlyList<string>> InstallAsync(
        FeatureContext context, CancellationToken cancellationToken = default)
    {
        var steps = new List<string>();

        var apiCsproj = CleanArchitectureLayout.ApiCsproj(context);
        var apiProjectPath = CleanArchitectureLayout.ApiProjectPath(context);

        DotnetCli.Run(["add", apiCsproj, "package", "OpenTelemetry.Extensions.Hosting"], context.RootPath);
        steps.Add("Added OpenTelemetry hosting");

        DotnetCli.Run(["add", apiCsproj, "package", "OpenTelemetry.Instrumentation.AspNetCore"], context.RootPath);
        steps.Add("Added ASP.NET Core instrumentation");

        DotnetCli.Run(["add", apiCsproj, "package", "OpenTelemetry.Exporter.Console"], context.RootPath);
        steps.Add("Added console exporter");

        ProgramFileEditor.AddUsingsAndServiceRegistration(
            Path.Combine(apiProjectPath, "Program.cs"),
            usings: ["OpenTelemetry.Resources", "OpenTelemetry.Metrics", "OpenTelemetry.Trace"],
            serviceRegistrationLines:
            [
                "builder.Services.AddOpenTelemetry()",
                $"    .ConfigureResource(resource => resource.AddService(\"{context.ProjectName}\"))",
                "    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddConsoleExporter())",
                "    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddConsoleExporter());",
            ]);
        steps.Add("Added tracing and metrics configuration");

        return Task.FromResult<IReadOnlyList<string>>(steps);
    }
}
