namespace AspireForge.Core.Features;

public interface IFeatureInstaller
{
    FeatureDefinition Feature { get; }

    Task InstallAsync(FeatureContext context, CancellationToken cancellationToken = default);
}
