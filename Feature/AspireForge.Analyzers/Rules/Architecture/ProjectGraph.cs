using System.Xml;
using System.Xml.Linq;

namespace AspireForge.Analyzers.Rules.Architecture;

// The project-reference graph reachable from the analysed project, shared by the architecture rules so
// they agree on which projects exist. Which layer each one is depends on the architecture - see
// ArchitectureModel.Classify.
internal static class ProjectGraph
{
    public sealed record Project(string File, string Name, XDocument Document);

    // Breadth-first from `startProjectFile` through <ProjectReference>s. Missing or malformed project
    // files are skipped, and cycles are visited once.
    public static IEnumerable<Project> Walk(string startProjectFile, CancellationToken cancellationToken = default)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>([Path.GetFullPath(startProjectFile)]);

        while (pending.TryDequeue(out var projectFile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!visited.Add(projectFile) || Load(projectFile) is not { } document)
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(projectFile);
            yield return new Project(projectFile, name, document);

            foreach (var reference in Elements(document, "ProjectReference"))
            {
                pending.Enqueue(ResolveReference(projectFile, reference));
            }
        }
    }

    // Elements with a non-empty Include, matched by local name so projects using the legacy MSBuild XML
    // namespace are read too.
    public static IEnumerable<XElement> Elements(XDocument document, string localName) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == localName
                && !string.IsNullOrWhiteSpace(element.Attribute("Include")?.Value));

    public static string ResolveReference(string projectFile, XElement reference) =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(projectFile)!,
            reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)));

    public static int? LineOf(XElement element) =>
        ((IXmlLineInfo)element).HasLineInfo() ? ((IXmlLineInfo)element).LineNumber : null;

    private static XDocument? Load(string projectFile)
    {
        try
        {
            return File.Exists(projectFile) ? XDocument.Load(projectFile, LoadOptions.SetLineInfo) : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
