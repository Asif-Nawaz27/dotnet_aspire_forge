namespace AspireForge.Analyzers.Tests;

public class ProjectContextBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_ParsesTargetFrameworkAndReferences()
    {
        var apiDirectory = Path.Combine(_root, "Api");
        Directory.CreateDirectory(apiDirectory);
        File.WriteAllText(
            Path.Combine(apiDirectory, "Api.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
                <ProjectReference Include="../ServiceDefaults/ServiceDefaults.csproj" />
              </ItemGroup>
            </Project>
            """);

        var context = ProjectContextBuilder.Build(apiDirectory);

        Assert.Equal("net10.0", context.TargetFramework);
        Assert.Contains("Swashbuckle.AspNetCore", context.PackageReferences);
        Assert.Contains("../ServiceDefaults/ServiceDefaults.csproj", context.ProjectReferences);
    }

    [Fact]
    public void Build_ReadsEveryFramework_WhenMultiTargeting()
    {
        var apiDirectory = WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0; net10.0;$(ExtraFramework)</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);

        var context = ProjectContextBuilder.Build(apiDirectory);

        Assert.Equal(["net8.0", "net10.0"], context.TargetFrameworks);
        Assert.Equal("net8.0", context.TargetFramework);
    }

    [Fact]
    public void Build_FallsBackToTheNearestDirectoryBuildProps()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var apiDirectory = WriteProject("""<Project Sdk="Microsoft.NET.Sdk.Web" />""");

        var context = ProjectContextBuilder.Build(apiDirectory);

        Assert.Equal(["net9.0"], context.TargetFrameworks);
    }

    [Fact]
    public void Build_PrefersTheProjectFile_OverDirectoryBuildProps()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, "Directory.Build.props"),
            "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
        var apiDirectory = WriteProject(
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        var context = ProjectContextBuilder.Build(apiDirectory);

        Assert.Equal(["net10.0"], context.TargetFrameworks);
    }

    [Fact]
    public void Build_ReadsLegacyProjectsUsingTheMsBuildNamespace()
    {
        var apiDirectory = WriteProject("""
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <TargetFramework>net6.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var context = ProjectContextBuilder.Build(apiDirectory);

        Assert.Equal(["net6.0"], context.TargetFrameworks);
    }

    private string WriteProject(string csproj)
    {
        var apiDirectory = Path.Combine(_root, "Api");
        Directory.CreateDirectory(apiDirectory);
        File.WriteAllText(Path.Combine(apiDirectory, "Api.csproj"), csproj);
        return apiDirectory;
    }

    [Fact]
    public void Build_IncludesSourceFilesFromReferencedProjects()
    {
        var apiDirectory = Path.Combine(_root, "Api");
        var serviceDefaultsDirectory = Path.Combine(_root, "ServiceDefaults");
        Directory.CreateDirectory(apiDirectory);
        Directory.CreateDirectory(serviceDefaultsDirectory);

        File.WriteAllText(
            Path.Combine(apiDirectory, "Api.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <ProjectReference Include="../ServiceDefaults/ServiceDefaults.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(apiDirectory, "Program.cs"), "// api");
        File.WriteAllText(Path.Combine(serviceDefaultsDirectory, "Extensions.cs"), "// service defaults");

        var context = ProjectContextBuilder.Build(apiDirectory);

        Assert.Contains(context.SourceFiles, file => file.EndsWith("Program.cs"));
        Assert.Contains(context.SourceFiles, file => file.EndsWith("Extensions.cs"));
    }

    [Fact]
    public void Build_ExcludesBinAndObjDirectories()
    {
        var apiDirectory = Path.Combine(_root, "Api");
        Directory.CreateDirectory(Path.Combine(apiDirectory, "obj"));
        Directory.CreateDirectory(Path.Combine(apiDirectory, "bin"));
        File.WriteAllText(Path.Combine(apiDirectory, "Api.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(apiDirectory, "Program.cs"), "// api");
        File.WriteAllText(Path.Combine(apiDirectory, "obj", "Generated.cs"), "// generated");
        File.WriteAllText(Path.Combine(apiDirectory, "bin", "Copied.cs"), "// copied");

        var context = ProjectContextBuilder.Build(apiDirectory);

        Assert.DoesNotContain(context.SourceFiles, file => file.Contains("Generated.cs"));
        Assert.DoesNotContain(context.SourceFiles, file => file.Contains("Copied.cs"));
        Assert.Contains(context.SourceFiles, file => file.EndsWith("Program.cs"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
