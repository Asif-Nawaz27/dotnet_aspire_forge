using AspireForge.Core.Fixes;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Fixes;

public sealed class REL001HealthChecksFix : IRuleFix
{
    public string RuleId => "REL001";

    public string Description => "Adds health checks (AddHealthChecks/MapHealthChecks) directly to the Api.";

    public Task<IReadOnlyList<string>> ApplyAsync(FixWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var programCsPath = Path.Combine(CleanArchitectureLayout.ApiProjectPath(workspace.Context), "Program.cs");

        workspace.Edit(programCsPath, lines =>
        {
            ProgramFileEditor.AddUsingsAndServiceRegistration(
                lines,
                usings: [],
                serviceRegistrationLines: ["builder.Services.AddHealthChecks();"]);

            ProgramFileEditor.InsertBeforeAppRun(lines, ["app.MapHealthChecks(\"/health\");"]);
        });

        return Task.FromResult<IReadOnlyList<string>>(
            ["Added builder.Services.AddHealthChecks()", "Added app.MapHealthChecks(\"/health\")"]);
    }
}
