# AspireForge

Opinionated .NET Aspire project scaffolding and analysis CLI.

## Install

```bash
dotnet tool install --global AspireForge.Cli
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

## License

MIT
