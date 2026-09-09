# Contributing to AspireForge

Thanks for your interest in contributing.

## Getting started

1. Install the .NET SDK version pinned in [global.json](global.json).
2. Restore and build: `dotnet build`
3. Run the tests: `dotnet test`

## Project layout

- `feature/` — the CLI, Core, Analyzers, and Generators projects
- `tests/` — unit and integration tests, one project per `feature/` project
- `samples/` — example projects generated with AspireForge
- `docs/` — user and contributor documentation

## Submitting changes

- Open an issue before starting significant work.
- Keep pull requests focused and include tests for behavior changes.
- Ensure `dotnet build` and `dotnet test` pass before submitting.
