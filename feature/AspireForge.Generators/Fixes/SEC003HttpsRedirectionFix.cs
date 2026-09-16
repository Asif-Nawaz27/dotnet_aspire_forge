using AspireForge.Core.Features;
using AspireForge.Core.Fixes;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Fixes;

public sealed class SEC003HttpsRedirectionFix : IRuleFix
{
    public string RuleId => "SEC003";

    public string Description => "Adds app.UseHttpsRedirection() to the Api.";

    public Task<IReadOnlyList<string>> ApplyAsync(FeatureContext context, CancellationToken cancellationToken = default)
    {
        var programCsPath = Path.Combine(CleanArchitectureLayout.ApiProjectPath(context), "Program.cs");

        ProgramFileEditor.InsertAfterBuilderBuild(programCsPath, ["app.UseHttpsRedirection();"]);

        return Task.FromResult<IReadOnlyList<string>>(["Added app.UseHttpsRedirection()"]);
    }
}
