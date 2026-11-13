using System.Text.RegularExpressions;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Tests;

public class FrameworkPackagesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    public FrameworkPackagesTests() => Directory.CreateDirectory(_root);

    // The regression: 11.0.0 was picked for a net10.0 project.
    [Fact]
    public void PicksTheNewestStableRelease_ForTheProjectsDotNetVersion() =>
        Assert.Equal("10.0.12", FrameworkPackages.NewestStableWithMajor(["9.0.5", "10.0.3", "10.0.12", "10.0.9", "11.0.0"], 10));

    // Ordering by text would put 10.0.9 after 10.0.12.
    [Fact]
    public void ComparesVersionsAsNumbers() =>
        Assert.Equal("10.0.12", FrameworkPackages.NewestStableWithMajor(["10.0.9", "10.0.12"], 10));

    [Fact]
    public void SkipsPrereleases() =>
        Assert.Equal("10.0.2", FrameworkPackages.NewestStableWithMajor(["10.0.2", "10.0.3-rc.1", "10.1.0-preview.2"], 10));

    [Fact]
    public void IgnoresBuildMetadata() =>
        Assert.Equal("10.0.4", FrameworkPackages.NewestStableWithMajor(["10.0.4+abc123"], 10));

    [Fact]
    public void FindsNothing_WhenNoReleaseHasTheMajor() =>
        Assert.Null(FrameworkPackages.NewestStableWithMajor(["9.0.1", "11.0.0"], 10));

    [Theory]
    [InlineData("Microsoft.AspNetCore.OpenApi", true)]
    [InlineData("Microsoft.AspNetCore.Authentication.JwtBearer", true)]
    [InlineData("Microsoft.Extensions.Http.Resilience", true)]
    [InlineData("Microsoft.Extensions.Caching.StackExchangeRedis", true)]
    [InlineData("Microsoft.EntityFrameworkCore.Design", true)]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer", true)]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", true)]
    [InlineData("OpenTelemetry.Extensions.Hosting", false)]
    [InlineData("Npgsql", false)]
    [InlineData("Serilog.AspNetCore", false)]
    public void KnowsWhichPackagesAreVersionedWithDotNet(string package, bool aligned) =>
        Assert.Equal(aligned, FrameworkPackages.IsFrameworkAligned(package));

    [Theory]
    [InlineData("<TargetFramework>net10.0</TargetFramework>", 10)]
    [InlineData("<TargetFramework>net8.0</TargetFramework>", 8)]
    [InlineData("<TargetFrameworks>net10.0;net9.0</TargetFrameworks>", 9)]
    [InlineData("<TargetFramework>net10.0-windows</TargetFramework>", 10)]
    [InlineData("<TargetFramework>netstandard2.0</TargetFramework>", null)]
    [InlineData("<TargetFramework>$(DefaultFramework)</TargetFramework>", null)]
    [InlineData("", null)]
    public void ReadsTheTargetMajorVersion(string property, int? expected)
    {
        var csproj = Path.Combine(_root, "App.csproj");
        File.WriteAllText(csproj, $"<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    {property}\n  </PropertyGroup>\n</Project>\n");

        Assert.Equal(expected, FrameworkPackages.TargetMajor(csproj));
    }

    // Regression test: `new` passes the Api's directory, as `dotnet add` allows, and reading the directory as
    // a file failed generation with "Access to the path is denied".
    [Fact]
    public void ReadsTheTargetMajorVersion_FromAProjectDirectory()
    {
        File.WriteAllText(Path.Combine(_root, "App.csproj"), "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        Assert.Equal(10, FrameworkPackages.TargetMajor(_root));
    }

    [Fact]
    public void ReadsNoTargetMajorVersion_FromADirectoryWithoutAProject() =>
        Assert.Null(FrameworkPackages.TargetMajor(_root));

    [Fact]
    public void ReadsVersions_FromEverySource_ForExactlyThatPackage()
    {
        const string Json = """
            {
              "version": 2,
              "problems": [],
              "searchResult": [
                { "sourceName": "Company feed", "packages": [] },
                { "sourceName": "Private", "packages": [ { "id": "microsoft.aspnetcore.openapi", "version": "10.0.20" } ] },
                { "sourceName": "nuget.org", "packages": [
                    { "id": "Microsoft.AspNetCore.OpenApi", "version": "10.0.9" },
                    { "id": "Microsoft.AspNetCore.OpenApi.Extra", "version": "99.0.0" } ] }
              ]
            }
            """;

        Assert.Equal(["10.0.20", "10.0.9"], FrameworkPackages.VersionsFromSearch(Json, "Microsoft.AspNetCore.OpenApi"));
    }

    [Fact]
    public void ReadsNoVersions_FromASearchWithoutResults() =>
        Assert.Empty(FrameworkPackages.VersionsFromSearch("""{ "version": 2, "problems": [] }""", "X"));

    // A raw `dotnet add package` without a version is exactly what broke every generated project.
    [Fact]
    public void EveryPackageAdd_GoesThroughFrameworkPackages()
    {
        var generators = FindDirectory(Path.Combine("Feature", "AspireForge.Generators"));
        var unversioned = Directory.EnumerateFiles(generators, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => Path.GetFileName(file) != "FrameworkPackages.cs")
            .SelectMany(file => File.ReadLines(file).Select((line, i) => (File: Path.GetFileName(file), Line: i + 1, Text: line)))
            .Where(line => Regex.IsMatch(line.Text, @"""package"",") && !line.Text.Contains("\"--version\"", StringComparison.Ordinal))
            .Select(line => $"{line.File}:{line.Line}")
            .ToList();

        Assert.True(unversioned.Count == 0, "Unversioned `dotnet add package` calls:\n" + string.Join("\n", unversioned));
    }

    private static string FindDirectory(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relative);

            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"Could not find {relative} above the test output.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
