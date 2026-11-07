using System.Reflection;

namespace AspireForge.Generators.GitHub;

// The version of AspireForge doing the generating. A generated workflow installs exactly this version:
// anyone who installed the tool from NuGet is running a version NuGet has, so the install can't ask for
// a release that doesn't exist, and CI keeps running the rules the project was generated with until
// someone chooses to move it. The version is set once for every assembly, in Directory.Build.props.
public static class ToolVersion
{
    public static string Current { get; } = Read(typeof(ToolVersion).Assembly);

    // "0.1.0+40b6ccb" - the part after the plus is the commit, which isn't part of the package version.
    public static string Read(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? throw new InvalidOperationException($"{assembly.GetName().Name} has no version.");
}
