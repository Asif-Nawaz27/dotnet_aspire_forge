using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForge.Generators.Project;

internal static class AppSettingsEditor
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static void AddConnectionString(string appsettingsPath, string name, string value)
    {
        var root = File.Exists(appsettingsPath)
            ? JsonNode.Parse(File.ReadAllText(appsettingsPath)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        if (root["ConnectionStrings"] is not JsonObject connectionStrings)
        {
            connectionStrings = new JsonObject();
            root["ConnectionStrings"] = connectionStrings;
        }

        connectionStrings[name] = value;

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
