namespace AspireForge.Core.Features;

public interface IFeatureInstaller
{
    FeatureDefinition Feature { get; }

    Task<IReadOnlyList<string>> InstallAsync(FeatureContext context, CancellationToken cancellationToken = default);
}
