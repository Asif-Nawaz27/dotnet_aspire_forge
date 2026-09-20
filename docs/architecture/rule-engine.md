# Rule engine

## `IAnalysisRule`

Every rule is an independent implementation of one interface:

```csharp
public interface IAnalysisRule
{
    string Id { get; }
    string Title { get; }
    Severity DefaultSeverity { get; }

    Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default);
}
```

`EvaluateAsync` returns `null` when there's nothing to report, or an `AnalysisIssue` describing
what's wrong. Rules don't know about each other, about `doctor`, or about how their result will be
displayed - that's deliberate, so a rule can be tested in isolation and so third-party rules could
plug into the same contract in the future (see [`rules list`](#custom-rules) below).

## `ProjectContext`

The input every rule receives:

| Property | Description |
|---|---|
| `RootDirectory`, `ProjectFile`, `ProjectName` | Where the project lives and what it's called. |
| `TargetFramework` | Parsed from the `.csproj`. |
| `SourceFiles` | Every `.cs` file under the project, **plus** every `.cs` file under any directly-referenced project (so a rule looking for `AddOpenTelemetry(` still sees it if that call lives in a referenced `ServiceDefaults` project rather than the analyzed project itself). |
| `ProjectReferences`, `PackageReferences` | Read directly from the `.csproj` - these stay project-local, since a rule like ARCH001 needs to know what the analyzed project *itself* references, not what a referenced project brings in. |

Most rules use the shared `SourceFileHeuristics` helper to check whether the project looks like an
ASP.NET Core web app (`WebApplication.CreateBuilder` present) and to search source text for a
token, rather than re-implementing file scanning themselves. That search strips `//` and `/* */`
comments first, so a token only mentioned in a comment doesn't count as real usage - this is still
plain text search, not a C# parser, so it doesn't understand string literals (a URL's `://` is
specifically guarded against, since it's the common case, but it isn't a general solution).
`SourceFileHeuristics.OwnSourceFiles` narrows `SourceFiles` down to just the analyzed project's own
files, for rules like ARCH001 that need to tell "this project's own code uses X" apart from "X is
merely reachable through `SourceFiles`'s referenced-project files."

## `ProjectAnalyzer`

Runs a given set of rules against a project and aggregates the results:

1. `LoadProject`/`BuildProjectContext` - resolve the path to a `.csproj` and build its `ProjectContext`.
2. `LoadRules` - the rules it was constructed with.
3. `ExecuteRules` - run every rule concurrently (`Task.WhenAll`) and keep the non-null results.
4. `ReturnAnalysisResult` - `new AnalysisResult(context.ProjectName, issues)`.

`AnalysisRuleSet.CreateDefault()` is the single place that lists the built-in rule set.

## Configuration

`.aspireforge/config.json`, discovered by walking upward from the analyzed project's directory:

```json
{
  "rules": {
    "SEC001": { "severity": "error" },
    "TEST002": { "enabled": false }
  }
}
```

Two pure functions apply it, both in `AnalysisRuleSet`:

- `ApplyConfig(rules, config)` - filters out rules explicitly set to `"enabled": false` *before*
  they run.
- `ApplySeverityOverrides(result, config)` - remaps the severity of matching issues *after*
  analysis.

`ProjectAnalyzer` itself stays config-agnostic; `doctor` composes these two functions around it.
Overrides apply identically across all three of `doctor`'s output formats, since text/JSON/SARIF
all render from the same config-adjusted `AnalysisResult`.

## Custom rules

`aspireforge rules list` prints the full catalog (id, category, short name) from the same source
`doctor` uses, so the two can never drift apart. A `rules install <pack>` command for third-party
rule packages is a natural extension of the `IAnalysisRule` contract, but isn't implemented yet.
