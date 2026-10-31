using System.Text;
using System.Text.Json;

namespace AspireForge.Analyzers.Rules;

// Reads appsettings*.json for rules that inspect configuration values (SEC004, OBS007). Walks the JSON
// with a reader rather than a DOM so every value keeps its line number. Tolerates what the ASP.NET
// Core configuration loader tolerates: comments, trailing commas, and a UTF-8 BOM.
internal static class JsonSettingsReader
{
    public const string DevelopmentFileName = "appsettings.Development.json";

    // A string value, its full configuration path ("Logging:LogLevel:Default"), and the key judged for
    // it - its own name, or for an array item the containing key.
    public sealed record Setting(string Path, string Key, string Value, int Line);

    // The project's appsettings*.json files, ordered by name. appsettings.Development.json is excluded by
    // default: development-only values never reach production.
    public static IEnumerable<string> ConfigurationFiles(string rootDirectory, bool includeDevelopment = false) =>
        Directory.Exists(rootDirectory)
            ? Directory.EnumerateFiles(rootDirectory, "appsettings*.json", SearchOption.TopDirectoryOnly)
                .Where(file => includeDevelopment
                    || !Path.GetFileName(file).Equals(DevelopmentFileName, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
            : [];

    // Every string value in the file. Malformed JSON stops the walk and returns what was read before
    // the error - the app fails to start on it anyway, which isn't a configuration rule's concern.
    public static List<Setting> ReadStrings(byte[] content)
    {
        var settings = new List<Setting>();
        var json = StripUtf8Bom(content);

        var reader = new Utf8JsonReader(json, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var path = new List<string>();
        string? propertyName = null;

        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        propertyName = reader.GetString();
                        break;

                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        path.Add(propertyName ?? string.Empty);
                        propertyName = null;
                        break;

                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        path.RemoveAt(path.Count - 1);
                        propertyName = null;
                        break;

                    case JsonTokenType.String:
                        settings.Add(new Setting(
                            FormatPath(path, propertyName),
                            propertyName ?? path.LastOrDefault() ?? string.Empty,
                            reader.GetString() ?? string.Empty,
                            LineOf(json, reader.TokenStartIndex)));
                        propertyName = null;
                        break;

                    default:
                        propertyName = null;
                        break;
                }
            }
        }
        catch (JsonException)
        {
        }

        return settings;
    }

    private static ReadOnlySpan<byte> StripUtf8Bom(byte[] content) =>
        content.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? content.AsSpan(Encoding.UTF8.Preamble.Length) : content;

    private static int LineOf(ReadOnlySpan<byte> json, long index) =>
        json[..(int)index].Count((byte)'\n') + 1;

    private static string FormatPath(List<string> path, string? propertyName) =>
        string.Join(':', path.Append(propertyName ?? string.Empty).Where(segment => segment.Length > 0));
}
