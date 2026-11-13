using AspireForge.Core.Text;

namespace AspireForge.Generators.Project;

// Edits Program.cs for `add`, `update`, and `fix`. The path overloads edit the file in place through
// TextFile, which keeps its line endings; the list overloads do the same edit on lines a FixWorkspace holds.
internal static class ProgramFileEditor
{
    public static void AddUsingsAndServiceRegistration(
        string programCsPath, IReadOnlyList<string> usings, IReadOnlyList<string> serviceRegistrationLines) =>
        TextFile.Edit(programCsPath, lines => AddUsingsAndServiceRegistration(lines, usings, serviceRegistrationLines));

    public static void AddUsingsAndServiceRegistration(
        List<string> lines, IReadOnlyList<string> usings, IReadOnlyList<string> serviceRegistrationLines)
    {
        InsertMissingUsings(lines, usings);
        InsertAfter(lines, "WebApplication.CreateBuilder", serviceRegistrationLines);
    }

    public static void InsertAfterBuilderBuild(string programCsPath, IReadOnlyList<string> lines) =>
        TextFile.Edit(programCsPath, fileLines => InsertAfterBuilderBuild(fileLines, lines));

    public static void InsertAfterBuilderBuild(List<string> fileLines, IReadOnlyList<string> lines) =>
        InsertAfter(fileLines, "builder.Build()", lines);

    // Middleware that must run before a later middleware (e.g. authentication before the rate limiter, as
    // SEC009 expects) goes just before `beforeAnchor` when it's present; otherwise - a project that never
    // added that middleware - just after builder.Build(), as before.
    public static void InsertBeforeOrAfterBuilderBuild(string programCsPath, string beforeAnchor, IReadOnlyList<string> lines) =>
        TextFile.Edit(programCsPath, fileLines =>
        {
            if (fileLines.Exists(line => line.Contains(beforeAnchor)))
            {
                InsertBefore(fileLines, beforeAnchor, lines);
            }
            else
            {
                InsertAfter(fileLines, "builder.Build()", lines);
            }
        });

    public static void InsertBeforeAppRun(string programCsPath, IReadOnlyList<string> lines) =>
        TextFile.Edit(programCsPath, fileLines => InsertBeforeAppRun(fileLines, lines));

    public static void InsertBeforeAppRun(List<string> fileLines, IReadOnlyList<string> lines) =>
        InsertBefore(fileLines, "app.Run(", lines);

    // Middleware that should run inside the exception handler goes after the whole UseExceptionHandler(...)
    // statement, so the handler stays first and catches what it throws. That's only safe when the handler
    // is at the same nesting as builder.Build(): one inside `if (!app.Environment.IsDevelopment()) { ... }`
    // would put the new line inside that block too, so then it goes after builder.Build(), as before.
    public static void InsertAfterExceptionHandlerOrBuilderBuild(List<string> fileLines, IReadOnlyList<string> lines)
    {
        var build = fileLines.FindIndex(line => line.Contains("builder.Build()"));
        var handler = fileLines.FindIndex(line => line.Contains(".UseExceptionHandler("));

        if (build >= 0 && handler > build && Depth(fileLines, handler) == Depth(fileLines, build))
        {
            fileLines.InsertRange(StatementEnd(fileLines, handler) + 1, lines.Prepend(string.Empty));
            return;
        }

        InsertAfter(fileLines, "builder.Build()", lines);
    }

    // How many braces are open before a line. Braces in strings and comments are counted too; Program.cs
    // rarely has any, and the fallback for a wrong answer is the old placement, not broken code.
    private static int Depth(List<string> lines, int index) =>
        lines.Take(index).Sum(line => line.Count(c => c == '{') - line.Count(c => c == '}'));

    // The last line of the statement starting at `start`: where its parentheses close and it ends with ';'.
    private static int StatementEnd(List<string> lines, int start)
    {
        var open = 0;

        for (var i = start; i < lines.Count; i++)
        {
            open += lines[i].Count(c => c == '(') - lines[i].Count(c => c == ')');

            if (open <= 0 && lines[i].TrimEnd().EndsWith(';'))
            {
                return i;
            }
        }

        return start;
    }

    private static void InsertMissingUsings(List<string> lines, IReadOnlyList<string> usings)
    {
        var missing = usings
            .Where(ns => lines.TrueForAll(line => line.Trim() != $"using {ns};"))
            .Select(ns => $"using {ns};")
            .ToList();

        if (missing.Count > 0)
        {
            lines.InsertRange(0, missing);
        }
    }

    private static void InsertAfter(List<string> lines, string anchor, IReadOnlyList<string> newLines)
    {
        var anchorIndex = lines.FindIndex(line => line.Contains(anchor));

        if (anchorIndex < 0)
        {
            throw new InvalidOperationException($"Could not find '{anchor}' in Program.cs.");
        }

        lines.InsertRange(anchorIndex + 1, newLines.Prepend(string.Empty));
    }

    private static void InsertBefore(List<string> lines, string anchor, IReadOnlyList<string> newLines)
    {
        var anchorIndex = lines.FindIndex(line => line.Contains(anchor));

        if (anchorIndex < 0)
        {
            throw new InvalidOperationException($"Could not find '{anchor}' in Program.cs.");
        }

        lines.InsertRange(anchorIndex, newLines.Append(string.Empty));
    }
}
