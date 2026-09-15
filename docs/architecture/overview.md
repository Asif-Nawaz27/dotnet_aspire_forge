# Architecture overview

## The tool itself

AspireForge is split into independent projects with a strict dependency direction:

```
AspireForge.Cli
      │
      ▼
AspireForge.Core
      │
      ├───────────────┐
      ▼               ▼
AspireForge.Analyzers  AspireForge.Generators
```

- **`AspireForge.Core`** - the domain model shared by everything else: `AnalysisIssue`,
  `AnalysisResult`, `ProjectContext`, `Severity`, `GenerationOptions`/`GenerationResult`,
  `FeatureContext`/`FeatureDefinition`/`IFeatureInstaller`, and the `.aspireforge/config.json`
  model. Has no dependencies of its own.
- **`AspireForge.Analyzers`** - the rule engine: the 10 `IAnalysisRule` implementations, the
  `ProjectAnalyzer` that runs them, and project/config discovery. See
  [Rule engine](rule-engine.md). Depends only on `Core`.
- **`AspireForge.Generators`** - project generation (`CleanArchitectureGenerator`) and every
  `IFeatureInstaller` (`postgres`, `sqlserver`, `redis`, `telemetry`, `docker`, `authentication`). Depends only
  on `Core`.
- **`AspireForge.Cli`** - the command-line surface, built on
  [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/). Depends on all
  three of the above, and is the only project published as a NuGet package (see
  [NuGet package strategy](#nuget-package-strategy) below).

`Analyzers` and `Generators` never depend on `Cli` or on each other - that's what keeps the rule
engine and the generator usable independently of the command-line tool.

## Generated projects

`aspireforge new` produces a clean-architecture solution:

```
ASP.NET Core (Api)
      │
      ▼
Application
      │
      ▼
Infrastructure
      │
      ▼
Domain
```

`Domain` has no outward dependencies. `Application` depends only on `Domain`. `Infrastructure`
depends on `Domain` and `Application`. `Api` (the ASP.NET Core host and composition root) depends
on `Application` and `Infrastructure`.

Rather than a bespoke templating engine, the generator composes the .NET SDK's own templates
(`dotnet new classlib`/`webapi`/`xunit`) and wires the project references between them - see
[`new`](../commands/new.md).

## Feature installers

Adding a capability to a generated project (`aspireforge add ...`) is implemented as an
`IFeatureInstaller`: a small, independent class that takes a `FeatureContext` (project name + root
path) and returns the list of steps it performed. `new` and `add` share this same abstraction -
`new` runs a fixed set of installers (README, `.gitignore`, Docker, CI) after generation, and `add`
runs one installer chosen by name. See [Features](../features/postgres.md) for what each one does.

## Configuration

A project can carry a `.aspireforge/config.json` file to enable/disable individual rules or
override their severity. See [Rule engine - configuration](rule-engine.md#configuration).

## Exit codes

Every command shares one exit-code scheme:

| Code | Meaning |
|---|---|
| `0` | Successful. |
| `1` | `doctor` found an issue at or above `--fail-on`. |
| `2` | Invalid command or configuration (bad arguments, missing solution file, unrecognized command). |
| `3` | Generation or feature-install failure. |

## NuGet package strategy

AspireForge ships as a single package today (`AspireForge`, the CLI). `Core`, `Analyzers`, and
`Generators` are internal-only (`IsPackable=false`) - that separation is what would let pieces like
the rule engine or the project templates become their own published packages later
(`AspireForge.Core`, `AspireForge.Analyzers`, `AspireForge.Rules`, `AspireForge.Templates`) without
a rewrite, but v0.1 intentionally ships one tool rather than a family of packages.
