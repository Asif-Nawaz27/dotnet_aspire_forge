# ProductionApi

Generated with AspireForge using the `clean` architecture template.

## Layout

- `src/ProductionApi.Domain` - entities and business rules with no outward dependencies.
- `src/ProductionApi.Application` - use cases and abstractions, depends only on Domain.
- `src/ProductionApi.Infrastructure` - implementations of Application abstractions (persistence, external services).
- `src/ProductionApi.Api` - the ASP.NET Core host and composition root.
- `tests/ProductionApi.IntegrationTests` - end-to-end tests against the Api.

## Getting started

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ProductionApi.Api
```

Run `aspireforge doctor src/ProductionApi.Api` to check the generated API against AspireForge's rule set.