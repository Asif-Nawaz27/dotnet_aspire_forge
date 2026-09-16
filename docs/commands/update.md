# `aspireforge update`

Refreshes the AspireForge-generated scaffolding in an existing project. Run it from the project
root (the directory containing the `.sln`/`.slnx` file).

## Usage

```bash
aspireforge update
```

## Example

```
$ aspireforge update
Updating Orders.Api

  ✓ Added .gitignore
  ✓ Added Dockerfile
  ✓ Added .dockerignore
  ✓ Added docker-compose.yml
  ✓ Added CI workflow

Update complete.
```

## What it touches

`update` re-runs the same deterministic, template-based installers `new` applies right after
generation:

| File | What decides its content |
|---|---|
| `.gitignore` | Fixed template for a multi-project .NET solution. |
| `Dockerfile`, `.dockerignore` | Fixed template for the generated `Api` project. |
| `docker-compose.yml` | Regenerated from scratch, aware of whichever databases/caches are already configured (reads `appsettings.json` connection strings) - the same logic [`add docker`](add.md) uses. |
| `.github/workflows/ci.yml` | Fixed CI template (restore/build/test on push to `main`). |

Each file is fully overwritten, not merged - useful for pulling in a newer AspireForge template, or
restoring a scaffolding file that got deleted or hand-edited into a broken state.

**`README.md` is deliberately left alone.** By the time a project is real enough to run `update`
against, its README has usually drifted into project-specific documentation, and silently
overwriting that would destroy real content rather than help. There's no flag to opt into
overwriting it - regenerate it by hand from [`ReadmeTemplate`](../../feature/AspireForge.Generators/Project/ReadmeTemplate.cs)
if you actually want that.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Update applied successfully. |
| `2` | No solution file was found in the current directory. |
| `3` | The update started but failed partway through. |
