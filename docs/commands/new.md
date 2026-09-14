# `aspireforge new`

Generates a new project.

## Usage

```bash
aspireforge new <name> [--architecture <name>]
```

| Argument/Option | Description | Default |
|---|---|---|
| `<name>` | The project name. Also used as the solution name and as a prefix for every generated project (`{name}.Api`, `{name}.Infrastructure`, ...). | *(required)* |
| `--architecture` | The architecture template to use. | `clean` |

## Architectures

Only `clean` is implemented today. Passing anything else fails with exit code `2`
(invalid configuration) and lists the supported values.

`clean` composes existing .NET templates (`dotnet new classlib`/`webapi`/`xunit`) rather than a
bespoke templating engine - see [Architecture overview](../architecture/overview.md). It produces:

```
<name>/
├── src/
│   ├── <name>.Domain/
│   ├── <name>.Application/
│   ├── <name>.Infrastructure/
│   └── <name>.Api/
├── tests/
│   └── <name>.IntegrationTests/
├── Dockerfile
├── .dockerignore
├── docker-compose.yml
├── .gitignore
├── README.md
├── <name>.slnx
└── .github/workflows/ci.yml
```

## Example

```bash
aspireforge new OrdersApi --architecture clean
```

```
Created OrdersApi (clean architecture):
  OrdersApi.slnx
  src/OrdersApi.Domain/
  src/OrdersApi.Application/
  src/OrdersApi.Infrastructure/
  src/OrdersApi.Api/
  tests/OrdersApi.IntegrationTests/
  Added README.md
  Added .gitignore
  Added Dockerfile
  Added .dockerignore
  Added docker-compose.yml
  Added CI workflow
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Generated successfully. |
| `2` | Invalid project name, or an unsupported `--architecture` value. |
| `3` | Generation started but failed (for example, the target directory already exists). |

See [Exit codes](../architecture/overview.md#exit-codes) for the full scheme shared by every command.
