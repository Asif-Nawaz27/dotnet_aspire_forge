# Using AspireForge on an existing API

AspireForge's six commands (`new`, `add`, `doctor`, `fix`, `update`, `rules`) split into two
groups by what they require when pointed at a project you didn't generate with `aspireforge new`.
`new` doesn't apply here - it only ever creates a fresh project.

## `doctor` and `rules list` work on any .NET project

[`doctor`](../commands/doctor.md) only needs a `.csproj` file (or a directory containing one). It
doesn't care how the project is laid out:

```bash
aspireforge doctor path/to/YourApi
```

This is the easiest way to try AspireForge against something you already have - it reads the
project file and source files and reports findings, it doesn't change anything.

`aspireforge rules list` doesn't touch a project at all; it just prints the static rule catalog.

## `add`, `fix`, and `update` expect the clean-architecture layout

[`add`](../commands/add.md), [`fix`](../commands/fix.md), and [`update`](../commands/update.md) all
locate your `Infrastructure` and `Api` projects by the same convention
(`src/{SolutionName}.Infrastructure`, `src/{SolutionName}.Api`, named after your solution file):

- `add` needs it to know which projects to wire a feature into.
- `fix` needs it to find `Api`'s `Program.cs` - both fixable rules (`REL001`, `SEC003`) edit that
  file directly.
- `update` needs it for the Docker installer specifically, which reads `Api/appsettings.json` to
  decide which services `docker-compose.yml` should include; its other installers (`.gitignore`,
  the CI workflow) don't depend on any particular layout.

If your repository doesn't follow that layout, none of the three will find the right project to
modify. For an existing project that doesn't match this convention, the practical options today
are:

- Rename/reorganize your projects to match the convention AspireForge expects, or
- Apply the changes `add`/`fix`/`update` would make by hand, using the
  [Features](../features/postgres.md) docs as a reference for what each one wires up.

Bringing these commands to arbitrary project layouts is a natural direction for AspireForge to
grow in, but isn't supported yet.

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
