using System.Text.Json;
using System.Text.Json.Serialization;
using AspireForge.Core.Configuration;

namespace AspireForge.Analyzers.Configuration;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AspireForgeConfig Load(string startDirectory)
    {
        var configPath = FindConfigFile(startDirectory);

        if (configPath is null)
        {
            return new AspireForgeConfig();
        }

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<AspireForgeConfig>(json, JsonOptions) ?? new AspireForgeConfig();
    }

    private static string? FindConfigFile(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".aspireforge", "config.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
