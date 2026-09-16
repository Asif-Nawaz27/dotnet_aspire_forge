using AspireForge.Core.Features;
using AspireForge.Core.Fixes;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Fixes;

public sealed class REL001HealthChecksFix : IRuleFix
{
    public string RuleId => "REL001";

    public string Description => "Adds health checks (AddHealthChecks/MapHealthChecks) directly to the Api.";

    public Task<IReadOnlyList<string>> ApplyAsync(FeatureContext context, CancellationToken cancellationToken = default)
    {
        var programCsPath = Path.Combine(CleanArchitectureLayout.ApiProjectPath(context), "Program.cs");

        ProgramFileEditor.AddUsingsAndServiceRegistration(
            programCsPath,
            usings: [],
            serviceRegistrationLines: ["builder.Services.AddHealthChecks();"]);

        ProgramFileEditor.InsertBeforeAppRun(programCsPath, ["app.MapHealthChecks(\"/health\");"]);

        return Task.FromResult<IReadOnlyList<string>>(
            ["Added builder.Services.AddHealthChecks()", "Added app.MapHealthChecks(\"/health\")"]);
    }
}
