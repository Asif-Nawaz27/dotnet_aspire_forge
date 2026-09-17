# BasicApi

Generated with AspireForge using the `clean` architecture template.

## Layout

- `src/BasicApi.Domain` - entities and business rules with no outward dependencies.
- `src/BasicApi.Application` - use cases and abstractions, depends only on Domain.
- `src/BasicApi.Infrastructure` - implementations of Application abstractions (persistence, external services).
- `src/BasicApi.Api` - the ASP.NET Core host and composition root.
- `tests/BasicApi.IntegrationTests` - end-to-end tests against the Api.

## Getting started

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/BasicApi.Api
```

Run `aspireforge doctor src/BasicApi.Api` to check the generated API against AspireForge's rule set.