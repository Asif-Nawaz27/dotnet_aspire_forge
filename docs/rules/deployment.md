# Deployment rules

## DEP001 - Unsupported .NET version

**Default severity:** `error` (out of support) / `warning` (ending within ~6 months)

A framework past end of support gets no security patches: every runtime vulnerability disclosed
after that date stays open in production. DEP001 checks each of the project's target frameworks
against Microsoft's published lifecycle dates
([.NET](https://dotnet.microsoft.com/platform/support/policy/dotnet-core),
[.NET Framework](https://learn.microsoft.com/lifecycle/products/microsoft-net-framework)):

- **error** - the framework is already out of support (e.g. `net6.0`, `net7.0`, `netcoreapp3.1`,
  `net461`).
- **warning** - the framework reaches end of support within the next ~6 months, so an upgrade can
  be planned before the deadline rather than after it. For example `net8.0` and `net9.0` both end
  support on 2026-11-10 (STS releases are supported for 24 months from .NET 9 onward).

Frameworks are read from `<TargetFramework>` or every entry of a multi-targeted
`<TargetFrameworks>`, falling back to the nearest `Directory.Build.props` when the project file
sets neither. OS-specific targets share their base framework's dates (`net8.0-windows` ends with
`net8.0`). Values built from MSBuild properties (`$(...)`) can't be evaluated and are skipped.

Not flagged: supported releases, frameworks newer than the lifecycle table AspireForge ships with,
.NET Framework 4.6.2 and later (supported as a Windows component), and `netstandard` (a library
contract, not a runtime).

**Fix:** Retarget to a supported release - the current LTS is `net10.0` - and update package
references to matching versions.

## DEP003 - Container image problems

**Default severity:** `warning` (`suggestion` for the last two checks)

Reviews the Dockerfile that builds this project. The Dockerfile is the nearest one found walking up
from the project directory to the repository root (the directory containing `.git`), so a
solution-level Dockerfile is found when you run `doctor` on the Api. Recognised names: `Dockerfile`,
`*.Dockerfile`, `Dockerfile.*`.

Not having a Dockerfile isn't a finding: plenty of apps deploy without containers, or build images
with the .NET SDK (`dotnet publish /t:PublishContainer`) instead.

The final stage is everything after the last `FROM`:

| Check | Severity |
| --- | --- |
| **Runs as root** - the final stage has no `USER` instruction, or `USER root` / `USER 0`. The .NET runtime images default to root; a compromised process then has full control of the container. Chiseled images (`*-chiseled*`) default to the non-root `app` user and are exempt. | warning |
| **SDK image in the final stage** - `mcr.microsoft.com/dotnet/sdk` ships compilers and build tools to production: a far larger image and attack surface. | warning |
| **Unpinned base image** - `:latest` or no tag, so the same Dockerfile builds different images over time. Earlier stage aliases, `scratch`, `${ARG}` images, and digest pins (`@sha256:...`) are fine. | suggestion |
| **No `.dockerignore`** next to a Dockerfile that copies the whole context (`COPY . .`, `ADD . /app`) - `.git`, `bin/obj`, and local secrets such as `appsettings.Development.json` are sent into the build. | suggestion |

Line continuations (`\`) and comments are handled; each problem is reported with its Dockerfile line,
most severe first.

**Fix:** Build in an SDK stage, run on the ASP.NET runtime image, and drop root:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Api/Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Api.dll"]
```

…with a `.dockerignore` listing at least `**/bin/`, `**/obj/`, and `.git/`.
