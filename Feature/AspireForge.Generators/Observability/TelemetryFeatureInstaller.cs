using AspireForge.Core.Features;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Observability;

// Follows Microsoft's Aspire ServiceDefaults convention rather than wiring OpenTelemetry into the
// Api's own Program.cs: a shared, referenceable project that configures metrics, traces, logs, and
// health checks in one place.
public sealed class TelemetryFeatureInstaller : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = "telemetry",
        Name = "OpenTelemetry",
        Description = "Adds a ServiceDefaults project configuring OpenTelemetry metrics/traces/logs and health checks.",
    };

    public async Task<IReadOnlyList<string>> InstallAsync(
        FeatureContext context, CancellationToken cancellationToken = default)
    {
        var steps = new List<string>();

        var serviceDefaultsPath = CleanArchitectureLayout.ServiceDefaultsProjectPath(context);
        var serviceDefaultsCsproj = CleanArchitectureLayout.ServiceDefaultsCsproj(context);
        var apiCsproj = CleanArchitectureLayout.ApiCsproj(context);
        var apiProjectPath = CleanArchitectureLayout.ApiProjectPath(context);

        CreateServiceDefaultsProject(context, serviceDefaultsPath, serviceDefaultsCsproj);
        steps.Add("Added ServiceDefaults project");

        AddOpenTelemetryPackages(context, serviceDefaultsCsproj);
        steps.Add("Configured metrics and traces");

        steps.Add("Configured structured logging");

        // MapHealthChecks/AddHealthChecks are provided transitively by the Microsoft.AspNetCore.App
        // FrameworkReference added above - no separate package needed.
        steps.Add("Configured health checks");

        await WriteServiceDefaultsExtensions(context, serviceDefaultsPath, cancellationToken);

        DotnetCli.Run(["add", apiCsproj, "reference", serviceDefaultsCsproj], context.RootPath);
        WireIntoApi(context, apiProjectPath);
        steps.Add("Wired ServiceDefaults into the Api");

        return steps;
    }

    private static void CreateServiceDefaultsProject(
        FeatureContext context, string serviceDefaultsPath, string serviceDefaultsCsproj)
    {
        var projectName = $"{context.ProjectName}.ServiceDefaults";

        DotnetCli.Run(["new", "classlib", "-n", projectName, "-o", serviceDefaultsPath], context.RootPath);

        var placeholder = Path.Combine(serviceDefaultsPath, "Class1.cs");
        if (File.Exists(placeholder))
        {
            File.Delete(placeholder);
        }

        DotnetCli.Run(["sln", "add", serviceDefaultsPath], context.RootPath);

        // Health check mapping (MapHealthChecks) and WebApplication both come from the ASP.NET Core
        // shared framework, which a plain classlib doesn't reference by default.
        CsprojEditor.AddFrameworkReference(serviceDefaultsCsproj, "Microsoft.AspNetCore.App");
    }

    private static void AddOpenTelemetryPackages(FeatureContext context, string serviceDefaultsCsproj)
    {
        foreach (var package in new[]
                 {
                     "OpenTelemetry.Extensions.Hosting",
                     "OpenTelemetry.Instrumentation.AspNetCore",
                     "OpenTelemetry.Instrumentation.Http",
                     "OpenTelemetry.Instrumentation.Runtime",
                     "OpenTelemetry.Exporter.OpenTelemetryProtocol",
                 })
        {
            DotnetCli.Run(["add", serviceDefaultsCsproj, "package", package], context.RootPath);
        }
    }

    private static async Task WriteServiceDefaultsExtensions(
        FeatureContext context, string serviceDefaultsPath, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            Path.Combine(serviceDefaultsPath, "Extensions.cs"),
            ServiceDefaultsTemplate.Extensions(context.ProjectName),
            cancellationToken);
    }

    private static void WireIntoApi(FeatureContext context, string apiProjectPath)
    {
        var programCsPath = Path.Combine(apiProjectPath, "Program.cs");

        ProgramFileEditor.AddUsingsAndServiceRegistration(
            programCsPath,
            usings: [$"{context.ProjectName}.ServiceDefaults"],
            serviceRegistrationLines: ["builder.AddServiceDefaults();"]);

        ProgramFileEditor.InsertBeforeAppRun(programCsPath, ["app.MapDefaultEndpoints();"]);
    }
}
