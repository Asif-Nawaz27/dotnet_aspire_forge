# `aspireforge fix`

Automatically fixes a single rule finding. Run it from the project root (the directory containing
the `.sln`/`.slnx` file).

## Usage

```bash
aspireforge fix <ruleId>
```

## Example

```
$ aspireforge fix REL001
Fix: REL001

  ✓ Added builder.Services.AddHealthChecks()
  ✓ Added app.MapHealthChecks("/health")

REL001 fixed successfully.
```

Re-running `aspireforge doctor` afterward no longer reports that rule.

## Fixable rules today

| Rule | What the fix does |
|---|---|
| REL001 | Adds `builder.Services.AddHealthChecks()` and `app.MapHealthChecks("/health")` directly to `Api`'s `Program.cs`. |
| SEC003 | Adds `app.UseHttpsRedirection()` to `Api`'s `Program.cs`. |

Most of the [10 rules](../architecture/rule-engine.md) don't have an automatic fix yet:

- **SEC001, OBS001, OBS002** already have a richer fix via [`add`](add.md) (`add authentication`,
  `add telemetry`) that does more than a one-line edit would.
- **ARCH001** is a structural refactor (move a dependency to a different project), not something
  safe to automate.
- **TEST001, TEST002** need a whole new test project generated, not a source edit.
- **SEC002, REL002** need a decision only you can make (what CORS origins to allow; what an
  exception handler should actually do), so there's no single safe default to apply.

Running `fix` against one of these prints the current list of fixable rule IDs and exits with code
`2` rather than guessing.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Fix applied successfully. |
| `2` | No fix is available for that rule ID, or no solution file was found in the current directory. |
| `3` | The fix started but failed partway through. |
