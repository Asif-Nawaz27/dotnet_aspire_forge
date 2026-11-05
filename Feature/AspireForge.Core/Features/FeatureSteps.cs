namespace AspireForge.Core.Features;

// How an installer reports that it had nothing to do because the feature is already there - so
// `aspireforge add` can say so instead of "added successfully".
public static class FeatureSteps
{
    private const string AlreadyInstalledPrefix = "Already configured: ";

    public static string AlreadyInstalled(string detail) => AlreadyInstalledPrefix + detail;

    public static bool IsAlreadyInstalled(IReadOnlyList<string> steps) =>
        steps.Count == 1 && steps[0].StartsWith(AlreadyInstalledPrefix, StringComparison.Ordinal);
}
