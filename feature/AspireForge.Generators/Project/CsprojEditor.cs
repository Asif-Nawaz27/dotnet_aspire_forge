using System.Xml.Linq;

namespace AspireForge.Generators.Project;

internal static class CsprojEditor
{
    public static void AddFrameworkReference(string csprojPath, string frameworkName)
    {
        var document = XDocument.Load(csprojPath);
        var root = document.Root!;

        var alreadyPresent = document.Descendants("FrameworkReference")
            .Any(element => element.Attribute("Include")?.Value == frameworkName);

        if (alreadyPresent)
        {
            return;
        }

        root.Add(new XElement("ItemGroup", new XElement("FrameworkReference", new XAttribute("Include", frameworkName))));
        document.Save(csprojPath);
    }
}
