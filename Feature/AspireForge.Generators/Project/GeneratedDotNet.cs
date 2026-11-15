namespace AspireForge.Generators.Project;

// The .NET version generated projects target, in one place. Everything generated has to agree on it: the
// projects' TargetFramework, the Dockerfile's SDK and runtime images, and the SDK the CI workflow installs.
//
// It's passed to every `dotnet new` explicitly. Left to the template, the framework is whatever the newest
// installed SDK defaults to: with the .NET 11 SDK installed next to .NET 10, projects came out as net11.0
// while the Dockerfile and CI stayed on 10.0, and the build failed (NETSDK1045).
public static class GeneratedDotNet
{
    public const int Major = 10;

    public static string TargetFramework => $"net{Major}.0";

    // Docker image tags and setup-dotnet versions use "10.0".
    public static string Channel => $"{Major}.0";
}
