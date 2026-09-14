# `aspireforge doctor`

Analyzes a project against AspireForge's [rule set](../architecture/rule-engine.md) and reports
what's missing.

## Usage

```bash
aspireforge doctor [<path>] [--fail-on <severity>] [--format <format>]
```

| Argument/Option | Description | Default |
|---|---|---|
| `<path>` | A project file, or a directory containing exactly one. | current directory |
| `--fail-on` | The minimum issue severity that causes a non-zero exit code: `info`, `suggestion`, `warning`, `error`, or `critical`. | `error` |
| `--format` | Output format: `text`, `json`, or `sarif`. | `text` |

## Text output (default)

```
AspireForge Doctor

Project: OrdersApi.Api
Framework: net10.0

Security
  ⚠ Authentication not configured
  ✓ CORS policy restricts origins
  ✓ HTTPS configured

Reliability
  ⚠ Health checks missing
  ⚠ Global exception handling missing

Observability
  ⚠ OpenTelemetry missing
  ⚠ Structured logging missing

Testing
  ✓ Unit tests detected
  ⚠ Integration tests not detected

Architecture
  ✓ No obvious dependency violations

--------------------------------
Errors:   0
Warnings: 6
Passed:   4
--------------------------------

Production Readiness: 4/10
```

`✓` means the rule found nothing to report. `⚠`/`✗` show the rule's title, using `✗` for
`error`/`critical` severity and `⚠` otherwise.

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
