using System.Text;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Tests;

public class ProgramFileEditorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    private string ProgramFile => Path.Combine(_root, "Program.cs");

    public ProgramFileEditorTests() => Directory.CreateDirectory(_root);

    private static readonly string[] Redirect = ["app.UseHttpsRedirection();"];

    private static List<string> Lines(string code) => [.. code.ReplaceLineEndings("\n").Split('\n')];

    // ---- Placement after the exception handler

    [Fact]
    public void KeepsTheExceptionHandlerFirst()
    {
        var lines = Lines("""
            var app = builder.Build();

            // First, so it also catches exceptions thrown by the middleware below.
            app.UseExceptionHandler();

            app.MapGet("/", () => "Hello");
            app.Run();
            """);

        ProgramFileEditor.InsertAfterExceptionHandlerOrBuilderBuild(lines, Redirect);

        Assert.True(lines.IndexOf("app.UseExceptionHandler();") < lines.IndexOf("app.UseHttpsRedirection();"));
        Assert.True(lines.IndexOf("app.UseHttpsRedirection();") < lines.IndexOf("app.MapGet(\"/\", () => \"Hello\");"));
    }

    // Inserting after the first line would land inside the lambda.
    [Fact]
    public void GoesAfterTheWholeStatement_WhenTheHandlerSpansLines()
    {
        var lines = Lines("""
            var app = builder.Build();
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(context => Task.CompletedTask);
            });
            app.Run();
            """);

        ProgramFileEditor.InsertAfterExceptionHandlerOrBuilderBuild(lines, Redirect);

        Assert.Equal(lines.IndexOf("});") + 2, lines.IndexOf("app.UseHttpsRedirection();"));
    }

    // Next to a handler inside `if`, the new line would only run in that branch.
    [Fact]
    public void GoesAfterBuilderBuild_WhenTheHandlerIsInsideABlock()
    {
        var lines = Lines("""
            var app = builder.Build();
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }
            app.Run();
            """);

        ProgramFileEditor.InsertAfterExceptionHandlerOrBuilderBuild(lines, Redirect);

        Assert.Equal(["var app = builder.Build();", "", "app.UseHttpsRedirection();", "if (!app.Environment.IsDevelopment())"], lines.Take(4));
    }

    [Fact]
    public void GoesAfterBuilderBuild_WhenThereIsNoHandler()
    {
        var lines = Lines("var app = builder.Build();\napp.Run();");

        ProgramFileEditor.InsertAfterExceptionHandlerOrBuilderBuild(lines, Redirect);

        Assert.Equal(["var app = builder.Build();", "", "app.UseHttpsRedirection();", "app.Run();"], lines);
    }

    // A handler registered before Build() is a different call (services.AddExceptionHandler is the norm,
    // but anything before the app exists can't be middleware).
    [Fact]
    public void IgnoresAMatchBeforeBuilderBuild()
    {
        var lines = Lines("builder.UseExceptionHandler(x);\nvar app = builder.Build();\napp.Run();");

        ProgramFileEditor.InsertAfterExceptionHandlerOrBuilderBuild(lines, Redirect);

        Assert.Equal(3, lines.IndexOf("app.UseHttpsRedirection();"));
    }

    [Fact]
    public void FailsClearly_WithoutBuilderBuild()
    {
        var lines = Lines("app.Run();");

        var exception = Assert.Throws<InvalidOperationException>(() => ProgramFileEditor.InsertAfterExceptionHandlerOrBuilderBuild(lines, Redirect));

        Assert.Contains("builder.Build()", exception.Message);
    }

    // ---- The path overloads `add` and `update` use

    // Regression test: File.WriteAllLines rewrote every line ending, so an LF file edited on Windows
    // (or a CRLF one on Linux) changed in full.
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void PathEdits_KeepTheFilesLineEndings(string newLine)
    {
        var original = string.Join(newLine, "var builder = WebApplication.CreateBuilder(args);", "var app = builder.Build();", "app.Run();", "");
        File.WriteAllText(ProgramFile, original);

        ProgramFileEditor.AddUsingsAndServiceRegistration(ProgramFile, ["Some.Namespace"], ["builder.Services.AddThing();"]);
        ProgramFileEditor.InsertAfterBuilderBuild(ProgramFile, ["app.UseThing();"]);
        ProgramFileEditor.InsertBeforeAppRun(ProgramFile, ["app.MapThing();"]);
        ProgramFileEditor.InsertBeforeOrAfterBuilderBuild(ProgramFile, "app.UseThing();", ["app.UseFirst();"]);

        var edited = File.ReadAllText(ProgramFile);
        var expected = edited.ReplaceLineEndings(newLine);

        Assert.Equal(expected, edited);
        Assert.EndsWith(newLine, edited);
        Assert.Contains("using Some.Namespace;", edited);
        Assert.Contains("app.MapThing();", edited);
    }

    [Fact]
    public void PathEdits_KeepTheByteOrderMark()
    {
        File.WriteAllText(ProgramFile, "var app = builder.Build();\napp.Run();\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        ProgramFileEditor.InsertAfterBuilderBuild(ProgramFile, ["app.UseThing();"]);

        Assert.True(File.ReadAllBytes(ProgramFile).AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
