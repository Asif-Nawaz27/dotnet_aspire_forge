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

        try
        {
            return JsonSerializer.Deserialize<AspireForgeConfig>(File.ReadAllText(configPath), JsonOptions) ?? new AspireForgeConfig();
        }
        // The serializer's own message names .NET types and byte positions; say what's wrong in the
        // file's terms instead.
        catch (JsonException exception) when (exception.Path == "$.architecture")
        {
            throw new InvalidDataException(
                $"The config at '{configPath}' has an unsupported \"architecture\". It takes \"clean\" or \"layered\" - "
                    + "see docs/rules/architecture.md.",
                exception);
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber is { } number ? $" on line {number + 1}" : string.Empty;

            throw new InvalidDataException($"The config at '{configPath}' isn't valid JSON{line}.", exception);
        }
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
