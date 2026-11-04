# `aspireforge doctor`

Analyzes a project against AspireForge's [rule set](../architecture/rule-engine.md) and reports
what's missing.

## Usage

```bash
aspireforge doctor [<path>] [--fail-on <severity>] [--format <format>] [--vulnerable] [--summary]
```

| Argument/Option | Description | Default |
|---|---|---|
| `<path>` | A project file, or a directory containing exactly one. | current directory |
| `--fail-on` | The minimum issue severity that causes a non-zero exit code: `info`, `suggestion`, `warning`, `error`, or `critical`. | `error` |
| `--format` | Output format: `text`, `json`, or `sarif`. | `text` |
| `--vulnerable` | Also scan packages, including transitive ones, for known security vulnerabilities ([DEP002](../rules/deployment.md#dep002---vulnerable-packages)). Runs `dotnet list package --vulnerable`, so it's slower and needs network access. | off |
| `--summary` | Text output only: show the checklist, totals, and next steps, without the per-finding details. | off |

## Text output (default)

The report has four parts. This is a freshly generated project (`aspireforge new Orders`), shortened
where marked `...`:

```
AspireForge Doctor

Project: Orders.Api
Framework: net10.0
Path: src/Orders.Api/Orders.Api.csproj

Security
  ⚠ Authentication not configured                                 SEC001
  ✓ CORS policy restricts origins
  ✓ HTTPS configured
  ⚠ HSTS missing                                                  SEC006
  ...

Observability
  ○ OpenTelemetry missing                                         OBS001
  ...

════════════════════════════ Findings (10) ════════════════════════════

⚠ DEP003 · Container image problems · warning
  Where  Dockerfile:8
  Why    Dockerfile has container problems: the final stage has no USER instruction, so the
         container runs as root - add `USER $APP_UID` (the non-root user the .NET images ship)
         (Dockerfile:8).
  Docs   https://github.com/Asif-Nawaz27/dotnet_aspire_forge/blob/main/docs/rules/deployment.md#dep003---container-image-problems

⚠ SEC001 · Authentication not configured · warning
  Scope  whole project
  Why    No call to AddAuthentication was found. If this API requires callers to be identified,
         register an authentication scheme.
  Fix    aspireforge add authentication
  Docs   https://github.com/Asif-Nawaz27/dotnet_aspire_forge/blob/main/docs/rules/security.md#sec001---authentication-not-configured
...

--------------------------------
Errors:      0
Warnings:    6
Suggestions: 4
Passed:      30
--------------------------------

Production Readiness: 8/10

Next steps
  aspireforge add authentication   resolves SEC001
  aspireforge fix REL001           resolves REL001
  aspireforge add telemetry        resolves OBS001, OBS002
  6 findings need a code change - see Why and Docs above.
```

1. **Checklist** - one line per enabled rule, grouped by category. `✓` means the rule found
   nothing; otherwise the line shows the finding's title and, on the right, its rule ID. The symbol
   shows severity: `✗` error/critical, `⚠` warning, `○` suggestion/info.
2. **Findings** - one block per finding, most severe first:

   | Label | Shows |
   | --- | --- |
   | `Where` | the file and line, relative to the current directory when it's inside it |
   | `Scope` | `whole project`, instead of `Where`, for findings about the project as a whole (something missing, a framework version) |
   | `Why` | what's wrong and why it matters, wrapped to the terminal width |
   | `Fix` | the single command that resolves it - `aspireforge fix <rule>` when there's an automatic fix, or `aspireforge add <feature>` when adding a feature satisfies the rule. Omitted when the fix is a code change |
   | `Docs` | the rule's section in these docs |

3. **Totals and score.**
4. **Next steps** - every one-command remedy, grouped by command, plus how many findings need a code
   change.

Pass `--summary` to skip the findings blocks when you only want the checklist and score.

## JSON output

```bash
aspireforge doctor --format json
```

```json
{
  "project": "OrdersApi.Api",
  "issues": [
    {
      "ruleId": "SEC001",
      "severity": "warning",
      "title": "Authentication not configured"
    }
  ]
}
```

## SARIF output

```bash
aspireforge doctor --format sarif
```

Produces a [SARIF 2.1.0](https://docs.oasis-open.org/sarif/sarif/v2.1.0/) log, for uploading to
GitHub code scanning:

```yaml
- name: Run AspireForge
  run: aspireforge doctor --format sarif > results.sarif

- name: Upload results
  uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: results.sarif
```

## Failing a CI pipeline on warnings

By default only `error`/`critical` issues cause a non-zero exit code. To also fail on warnings:

```bash
aspireforge doctor --fail-on warning
```

The printed report and JSON/SARIF payload are identical either way - `--fail-on` only changes the
exit code decision, never what's reported.

## Configuration

Rules can be enabled/disabled or have their severity overridden per project. See
[Configuration](../architecture/rule-engine.md#configuration).

## Exit codes

| Code | Meaning |
|---|---|
| `0` | No issue met the `--fail-on` threshold. |
| `1` | At least one issue met the `--fail-on` threshold. |
| `2` | The given path doesn't resolve to a project, or an invalid `--fail-on`/`--format` value was given. |
