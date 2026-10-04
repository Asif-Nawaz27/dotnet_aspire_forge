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
