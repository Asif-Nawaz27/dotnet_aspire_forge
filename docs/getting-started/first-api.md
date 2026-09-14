# Your first API

## Generate a project

```bash
aspireforge new OrdersApi
```

This creates an `OrdersApi/` directory using the `clean` architecture template (the only one
available today - see [`new`](../commands/new.md)):

```
OrdersApi/
├── src/
│   ├── OrdersApi.Domain/
│   ├── OrdersApi.Application/
│   ├── OrdersApi.Infrastructure/
│   └── OrdersApi.Api/
├── tests/
│   └── OrdersApi.IntegrationTests/
├── Dockerfile
├── .dockerignore
├── docker-compose.yml
├── .gitignore
├── README.md
├── OrdersApi.slnx
└── .github/
    └── workflows/
        └── ci.yml
```

`OrdersApi.Domain` has no dependencies. `OrdersApi.Application` depends only on `Domain`.
`OrdersApi.Infrastructure` depends on `Domain` and `Application`. `OrdersApi.Api` (the host, an
ASP.NET Core minimal API project) depends on `Application` and `Infrastructure`. See
[Architecture overview](../architecture/overview.md) for why.

## Run it

```bash
cd OrdersApi
dotnet run --project src/OrdersApi.Api
```

## Check it against AspireForge's rules

```bash
aspireforge doctor src/OrdersApi.Api
```

A freshly generated project reports several warnings (no authentication, no health checks, no
telemetry, and so on) - that's expected. See [`doctor`](../commands/doctor.md) for what each check
means and [`add`](../commands/add.md) for how to fix them.

## Add capabilities

```bash
aspireforge add postgres
aspireforge add telemetry
aspireforge add authentication
```

Run these from the project root (next to `OrdersApi.slnx`). See [Features](../features/postgres.md)
for what each one actually changes.

## Build and test

```bash
dotnet build
dotnet test
```
