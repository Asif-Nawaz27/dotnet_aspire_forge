# AspireForge

Opinionated .NET Aspire project scaffolding and analysis CLI.

## Install

```bash
dotnet tool install --global AspireForge
```

## Commands

```bash
aspireforge new <name> [--architecture clean]     # generate a new clean-architecture solution
aspireforge add <postgres|redis|telemetry|docker|authentication>  # add a feature to an existing project
aspireforge doctor [<path>] [--fail-on <severity>] [--format text|json|sarif]  # analyze a project
aspireforge rules list                            # list the analysis rule catalog
aspireforge version                               # show the CLI version
aspireforge help                                  # show help and usage information
```

## Configuration

Drop a `.aspireforge/config.json` at the root of a project to enable/disable rules or override
their severity:

```json
{
  "rules": {
    "SEC001": { "severity": "error" },
    "TEST002": { "enabled": false }
  }
}
```

## Packages

AspireForge ships as a single NuGet package (`AspireForge`, the global tool). Internally it's split
into separate class libraries (`AspireForge.Core`, `AspireForge.Analyzers`, `AspireForge.Generators`)
that stay unpublished for now - that modularity is what would let pieces like the rule engine or the
project templates become their own published packages later (e.g. `AspireForge.Core`,
`AspireForge.Analyzers`, `AspireForge.Rules`, `AspireForge.Templates`) without a rewrite, but v0.1
intentionally ships one tool rather than a family of packages.

## License

MIT
