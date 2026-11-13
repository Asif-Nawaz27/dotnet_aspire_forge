# `aspireforge fix`

Automatically fixes rule findings in the Api project. Run it from the project root (the directory
containing the `.sln`/`.slnx` file).

## Usage

```bash
aspireforge fix <ruleId> [--dry-run]
aspireforge fix --all [--dry-run]
```

| Argument/Option | Description |
|---|---|
| `<ruleId>` | The rule to fix, e.g. `REL001`. |
| `--all` | Fix every finding `doctor` reports that has an automatic fix. |
| `--dry-run` | Show the changes as a diff without writing anything. |

Give a rule ID or `--all`, not both.

## Example

```
$ aspireforge fix REL001
Fix: REL001

  ✓ Added builder.Services.AddHealthChecks()
  ✓ Added app.MapHealthChecks("/health")

REL001 fixed successfully.
```

Re-running `aspireforge doctor` afterward no longer reports that rule.

## Previewing changes

```
$ aspireforge fix --all --dry-run
Dry run: nothing was changed.

  REL001   Adds health checks (AddHealthChecks/MapHealthChecks) directly to the Api.
  SEC003   Adds app.UseHttpsRedirection() to the Api.

--- src/Orders.Api/Program.cs
+++ src/Orders.Api/Program.cs
@@ -1,7 +1,13 @@
 var builder = WebApplication.CreateBuilder(args);

+builder.Services.AddHealthChecks();
+
 var app = builder.Build();

+app.UseHttpsRedirection();
+
 app.MapGet("/", () => "Hello");

+app.MapHealthChecks("/health");
+
 app.Run();

1 file would change. Run without --dry-run to apply.
```

The diff is exactly what the same command without `--dry-run` writes. A preview exits `1` when it
would change something and `0` when there's nothing to fix, so it can fail a CI build:

```yaml
- name: No automatic fixes left undone
  run: aspireforge fix --all --dry-run
```

## What runs

A fix adds code, so it only runs when `doctor` reports its rule. Running `fix` on a project that
already passes changes nothing, however many times you run it:

```
$ aspireforge fix REL001
Nothing to fix: doctor doesn't report REL001.
```

- **`--all` follows `doctor` exactly.** Rules you've disabled and findings you've
  [accepted](doctor.md#accepting-findings), by inline comment or baseline, are left alone.
- **A named rule is an explicit request.** `fix SEC003` fixes a baselined SEC003 finding. It only
  needs the problem to really be there.

Either every fix succeeds or nothing is written, so a failure can't leave the project half-fixed.
Edits keep each file's line endings, so your own diff shows only the lines a fix changed.

## Fixable rules today

| Rule | What the fix does |
|---|---|
| REL001 | Adds `builder.Services.AddHealthChecks()` and `app.MapHealthChecks("/health")` directly to `Api`'s `Program.cs`. |
| SEC003 | Adds `app.UseHttpsRedirection()` to `Api`'s `Program.cs`, right after `app.UseExceptionHandler(...)` so the handler stays first - or after `builder.Build()` when there's no handler, or it's inside an `if` block. |

Most [rules](../architecture/rule-engine.md) don't have an automatic fix yet:

- **SEC001, OBS001, OBS002** already have a richer fix via [`add`](add.md) (`add authentication`,
  `add telemetry`) that does more than a one-line edit would.
- **ARCH001** is a structural refactor (move a dependency to a different project), not something
  safe to automate.
- **TEST001, TEST002** need a whole new test project generated, not a source edit.
- **SEC002, REL002** need a decision only you can make (what CORS origins to allow; what an
  exception handler should actually do), so there's no single safe default to apply.
- **PERF001, PERF002** (response compression, rate limiting) are realistic candidates for a future
  one-line fix like REL001's - just not implemented yet.
- **PERF003** (a blocking call found in your code) has no safe default rewrite; only you know what
  the correct `await`-based replacement should look like.

Running `fix` against one of these prints the current list of fixable rule IDs and exits with code
`2` rather than guessing.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Fixes applied, or there was nothing to fix. |
| `1` | `--dry-run` only: fixes are available, and the preview shows what they would change. Nothing was written. |
| `2` | No fix is available for that rule ID, neither or both of a rule ID and `--all` were given, no solution file or Api project was found, or `.aspireforge/baseline.json` can't be read. |
| `3` | A fix failed. Nothing was written. |
