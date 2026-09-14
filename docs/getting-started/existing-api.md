# Using AspireForge on an existing API

AspireForge's two commands have different requirements when pointed at a project you didn't
generate with `aspireforge new`.

## `doctor` works on any .NET project

[`doctor`](../commands/doctor.md) only needs a `.csproj` file (or a directory containing one). It
doesn't care how the project is laid out:

```bash
aspireforge doctor path/to/YourApi
```

This is the easiest way to try AspireForge against something you already have - it reads the
project file and source files and reports findings, it doesn't change anything.

## `add` expects the clean-architecture layout

[`add`](../commands/add.md) locates your `Infrastructure` and `Api` projects by convention
(`src/{SolutionName}.Infrastructure`, `src/{SolutionName}.Api`, named after your solution file).
If your repository doesn't follow that layout, `add` won't find the right projects to modify.

For an existing project that doesn't match this convention, the practical options today are:

- Rename/reorganize your projects to match the convention AspireForge expects, or
- Apply the changes `add` would make by hand, using the [Features](../features/postgres.md) docs
  as a reference for what each one wires up.

Bringing `add` to arbitrary project layouts is a natural direction for AspireForge to grow in, but
isn't supported yet.

## CI integration

Regardless of layout, you can run `doctor` in CI against any project:

```yaml
- name: AspireForge doctor
  run: |
    dotnet tool install --global AspireForge
    aspireforge doctor path/to/YourApi --format sarif > results.sarif

- uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: results.sarif
```
