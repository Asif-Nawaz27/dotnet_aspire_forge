using AspireForge.Core.Features;
using AspireForge.Generators.Docker;
using AspireForge.Generators.GitHub;
using AspireForge.Generators.Project;

namespace AspireForge.Generators;

// The subset of `new`'s post-generation installers (see CleanArchitectureGenerator.Features) that
// are safe to re-run, unattended, against an existing project: each one is a deterministic template
// render with no project-specific content. README is deliberately excluded - by the time a project
// is real enough to run `update` against, its README has usually drifted into project-specific
// documentation, and silently overwriting that would be destructive rather than helpful.
public static class UpdateFeatureSet
{
    public static IReadOnlyList<IFeatureInstaller> CreateDefault() =>
    [
        new GitIgnoreFeatureInstaller(),
        new DockerFeatureInstaller(),
        new GitHubActionsFeatureInstaller(),
    ];
}
