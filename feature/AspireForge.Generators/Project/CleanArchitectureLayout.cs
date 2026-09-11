using AspireForge.Core.Features;

namespace AspireForge.Generators.Project;

internal static class CleanArchitectureLayout
{
    public static string InfrastructureProjectPath(FeatureContext context) =>
        Path.Combine(context.RootPath, "src", $"{context.ProjectName}.Infrastructure");

    public static string ApiProjectPath(FeatureContext context) =>
        Path.Combine(context.RootPath, "src", $"{context.ProjectName}.Api");

    public static string InfrastructureCsproj(FeatureContext context) =>
        Path.Combine(InfrastructureProjectPath(context), $"{context.ProjectName}.Infrastructure.csproj");

    public static string ApiCsproj(FeatureContext context) =>
        Path.Combine(ApiProjectPath(context), $"{context.ProjectName}.Api.csproj");
}
