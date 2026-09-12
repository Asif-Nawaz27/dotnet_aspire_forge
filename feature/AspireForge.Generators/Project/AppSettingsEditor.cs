using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForge.Generators.Project;

internal static class AppSettingsEditor
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static void AddConnectionString(string appsettingsPath, string name, string value) =>
        AddSection(appsettingsPath, "ConnectionStrings", new Dictionary<string, string> { [name] = value });

    public static void AddSection(string appsettingsPath, string sectionName, IReadOnlyDictionary<string, string> values)
    {
        var root = File.Exists(appsettingsPath)
            ? JsonNode.Parse(File.ReadAllText(appsettingsPath)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        if (root[sectionName] is not JsonObject section)
        {
            section = new JsonObject();
            root[sectionName] = section;
        }

        foreach (var (key, value) in values)
        {
            section[key] = value;
        }

        File.WriteAllText(appsettingsPath, root.ToJsonString(WriteOptions));
    }

    public static bool HasConnectionString(string appsettingsPath, string name)
    {
        if (!File.Exists(appsettingsPath))
        {
            return false;
        }

        var root = JsonNode.Parse(File.ReadAllText(appsettingsPath)) as JsonObject;
        return root?["ConnectionStrings"] is JsonObject connectionStrings && connectionStrings.ContainsKey(name);
    }
}
