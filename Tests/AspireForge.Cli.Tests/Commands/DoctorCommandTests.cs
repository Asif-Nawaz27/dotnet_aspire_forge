using System.Text.Json;
using AspireForge.Cli.Commands;
using AspireForge.Cli.Output;
using AspireForge.Core.Analysis;

namespace AspireForge.Cli.Tests.Commands;

public class DoctorCommandTests : IDisposable
{
    private readonly string _projectDirectory =
        Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    public DoctorCommandTests()
    {
        Directory.CreateDirectory(_projectDirectory);
        File.WriteAllText(Path.Combine(_projectDirectory, "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        File.WriteAllText(Path.Combine(_projectDirectory, "Program.cs"), "var builder = WebApplication.CreateBuilder(args);");
    }

    [Fact]
    public async Task RunAsync_PrintsProjectNameAndDetectedIssues_ForTextFormat()
    {
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory);

        var output = writer.ToString();
        Assert.Contains("Project: Sample", output);
        Assert.Contains("Authentication not configured", output);
    }

    [Fact]
    public async Task RunAsync_ReturnsSuccess_WhenNoIssueMeetsTheDefaultErrorThreshold()
    {
        var command = new DoctorCommand(new ConsoleRenderer(new StringWriter()));

        var exitCode = await command.RunAsync(_projectDirectory);

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public async Task RunAsync_ReturnsAnalysisFoundErrors_WhenFailOnIsLoweredToWarning()
    {
        var command = new DoctorCommand(new ConsoleRenderer(new StringWriter()));

        var exitCode = await command.RunAsync(_projectDirectory, failOn: Severity.Warning);

        Assert.Equal(ExitCodes.AnalysisFoundErrors, exitCode);
    }

    [Fact]
    public async Task RunAsync_EmitsValidJson_ContainingTheDetectedIssues_ForJsonFormat()
    {
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory, format: OutputFormat.Json);

        using var document = JsonDocument.Parse(writer.ToString());
        var issues = document.RootElement.GetProperty("issues");

        Assert.True(issues.GetArrayLength() > 0);
        Assert.Contains(
            issues.EnumerateArray(),
            issue => issue.GetProperty("ruleId").GetString() == "SEC001");
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectDirectory))
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
    }
}
