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

## DEP002 - Vulnerable packages

**Default severity:** `error` (depends on the advisory) · **opt-in:** `aspireforge doctor --vulnerable`

Flags packages - direct **and transitive**, including those pulled in through project references -
with a published security advisory. It runs:

```bash
dotnet list <project> package --vulnerable --include-transitive --format json
```

which checks every resolved package against the vulnerability data of your configured NuGet sources
(nuget.org uses the GitHub Advisory Database). Because that runs the dotnet CLI, may restore the
project, and needs network access, DEP002 only runs when you pass `--vulnerable`. Without the flag
it's neither run nor shown, and doesn't count toward the score.

| Advisory severity | Finding |
| --- | --- |
| Critical, High | error |
| Moderate | warning |
| Low | suggestion |

The finding lists each package with its resolved version, advisory severity, whether it's transitive,
and the advisory URL - most severe first. If the scan itself can't run (no network, a restore
failure, the dotnet CLI unavailable), that's reported as an `info` finding - "Vulnerability scan
couldn't run" - rather than silently passing.

**Fix:** Upgrade each package to a patched version. For a transitive package, upgrade the direct
dependency that brings it in, or add a direct reference to the patched version. In CI, run
`aspireforge doctor --vulnerable` so a newly published advisory fails the build (errors exit non-zero
by default; see `--fail-on`).

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

## DEP004 - Prerelease or floating package versions

**Default severity:** `warning` (`suggestion` for floating versions)

Flags package versions that aren't stable releases:

| Version | Example | Severity |
| --- | --- | --- |
| **Prerelease** - any SemVer prerelease label | `10.0.0-preview.7`, `9.0.0-rc.2`, `1.0.0-beta.12`, `[1.0.0-beta, )` | warning |
| **Floating** - resolves to whatever is newest at restore time | `10.*`, `*`, `10.0.0-*` | suggestion |

A prerelease carries no support commitment: APIs can still change, bugs are expected, and security
fixes may only land in the next preview. A floating version means two builds of the same commit can
ship different code.

Projects checked: the analysed project **and every project it references**, transitively - their
packages ship in the same deployment. Versions are read from `<PackageReference Version="...">`
(attribute or child element), `VersionOverride`, and central package management (`<PackageVersion>`
in the nearest `Directory.Packages.props`). A central version used by several projects is reported
once, pointing at the props file.

Skipped:

- **Build-only packages** - `PrivateAssets="all"` (analyzers, `Microsoft.EntityFrameworkCore.Design`):
  they never reach production.
- **Versions built from MSBuild properties** (`$(EfCoreVersion)`): they can't be evaluated without
  MSBuild.

**Fix:** Pin every shipped package to an exact stable version. If a package only exists as a
prerelease (some OpenTelemetry instrumentation packages, for example), weigh that risk explicitly -
and disable the rule in `.aspireforge/config.json` if you accept it (see the [rule engine](../architecture/rule-engine.md)).
