# Installation

AspireForge is a [.NET global tool](https://learn.microsoft.com/dotnet/core/tools/global-tools),
distributed as the `AspireForge` NuGet package.

## Prerequisites

- The .NET 10 SDK (or later) installed and on your `PATH`.

## Install

```bash
dotnet tool install --global AspireForge
```

This installs a command named `aspireforge`.

## Verify

```bash
aspireforge version
```

```
aspireforge 0.1.0.0
```

## Upgrading

```bash
dotnet tool update --global AspireForge
```

## Uninstalling

```bash
dotnet tool uninstall --global AspireForge
```

## Next steps

- [Generate your first API](first-api.md)
- [Bring AspireForge to an existing API](existing-api.md)
