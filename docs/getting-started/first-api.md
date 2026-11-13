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
│   ├── OrdersApi.Api/
│   └── OrdersApi.ServiceDefaults/
├── tests/
│   └── OrdersApi.Api.IntegrationTests/
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
ASP.NET Core minimal API project) depends on `Application` and `Infrastructure`, plus
`OrdersApi.ServiceDefaults` - OpenTelemetry, health checks, and HTTP resilience, the .NET Aspire
convention. See [Architecture overview](../architecture/overview.md) for why.

The generated project already passes `aspireforge doctor` apart from authentication, which is your
call - see [Production-ready out of the box](../commands/new.md#production-ready-out-of-the-box).

## Run it

```bash
cd OrdersApi
dotnet run --project src/OrdersApi.Api
```

## Check it against AspireForge's rules

```bash
aspireforge doctor src/OrdersApi.Api
```

A freshly generated project passes every rule except SEC001 (authentication), which is your call -
`aspireforge add authentication` adds JWT bearer. See [`doctor`](../commands/doctor.md) for what each
check means, and [Production-ready out of the box](../commands/new.md#production-ready-out-of-the-box)
for what the generator sets up.

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
