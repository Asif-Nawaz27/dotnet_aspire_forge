using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// Flags deserializers that let the payload choose which .NET type to create. An attacker who controls
// the input can name a "gadget" type whose constructor or property setters run code - remote code
// execution from a crafted request body, cookie, cache entry, or queue message.
//
// Errors:
//   - BinaryFormatter, SoapFormatter, NetDataContractSerializer, LosFormatter, ObjectStateFormatter -
//     Microsoft's own guidance is that these can't be made safe for untrusted input
//     (https://learn.microsoft.com/dotnet/standard/serialization/binaryformatter-security-guide)
//   - JavaScriptSerializer constructed with a SimpleTypeResolver
//   - Newtonsoft.Json TypeNameHandling / ItemTypeNameHandling set to anything but None (All, Auto,
//     Objects, Arrays) - on JsonSerializerSettings, JsonSerializer, or a [JsonProperty] attribute
//     (code analysis rules CA2326/CA2327)
// Warning instead of error: TypeNameHandling in a file that also assigns a SerializationBinder, which
// can restrict the allowed types - the rule can't verify that the binder actually allow-lists them.
//
// Not flagged: System.Text.Json (it never reads type names from the payload; [JsonDerivedType]
// polymorphism is an explicit allow-list) and TypeNameHandling.None.
public sealed partial class SEC012UnsafeDeserialization : IAnalysisRule
{
    private const int MaxFindingsInDescription = 3;

    public string Id => "SEC012";

    public string Title => "Unsafe deserialization";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match match in InsecureFormatter().Matches(code))
            {
                findings.Add(new Finding(file, SourceFileHeuristics.LineOf(code, match.Index), Severity.Error,
                    $"{match.Groups["type"].Value} lets the payload choose which type to create"));
            }

            foreach (Match match in TypeResolvingJavaScriptSerializer().Matches(code))
            {
                findings.Add(new Finding(file, SourceFileHeuristics.LineOf(code, match.Index), Severity.Error,
                    "JavaScriptSerializer with SimpleTypeResolver creates whatever type the payload names"));
            }

            var hasBinder = SerializationBinderAssignment().IsMatch(code);

            foreach (Match match in TypeNameHandlingEnabled().Matches(code))
            {
                var setting = $"{match.Groups["property"].Value} = TypeNameHandling.{match.Groups["value"].Value}";

                findings.Add(hasBinder
                    ? new Finding(file, SourceFileHeuristics.LineOf(code, match.Index), Severity.Warning,
                        $"{setting} with a SerializationBinder - make sure the binder allow-lists the expected types")
                    : new Finding(file, SourceFileHeuristics.LineOf(code, match.Index), Severity.Error,
                        $"{setting} lets the payload's \"$type\" choose which type to create"));
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Line)
            .ToList();

        var listed = string.Join("; ", findings.Take(MaxFindingsInDescription).Select(finding => finding.Describe()));
        var more = findings.Count > MaxFindingsInDescription
            ? $" (and {findings.Count - MaxFindingsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"Deserialization trusts type information from the input: {listed}{more}. A crafted payload can "
                + "instantiate a gadget type and run code on the server. Use System.Text.Json (with [JsonDerivedType] "
                + "for polymorphism), or keep TypeNameHandling.None and deserialize into known types.",
            findings[0].Severity,
            findings[0].File,
            findings[0].Line);
    }

    private sealed record Finding(string File, int Line, Severity Severity, string Message)
    {
        public string Describe() => $"{Message} ({Path.GetFileName(File)}:{Line})";
    }

    // A type reference, not just the word: `new BinaryFormatter(`, `BinaryFormatter formatter`,
    // `typeof(BinaryFormatter)`, or a fully-qualified name - but not e.g. "MyBinaryFormatterWrapper".
    [GeneratedRegex(@"(?<![\w])(?<type>BinaryFormatter|SoapFormatter|NetDataContractSerializer|LosFormatter|ObjectStateFormatter)(?![\w])")]
    private static partial Regex InsecureFormatter();

    [GeneratedRegex(@"\bnew\s+JavaScriptSerializer\s*\(\s*new\s+SimpleTypeResolver\s*\(")]
    private static partial Regex TypeResolvingJavaScriptSerializer();

    // `TypeNameHandling = TypeNameHandling.Auto` - as a property assignment or a named attribute argument.
    [GeneratedRegex(@"\b(?<property>(?:Item)?TypeNameHandling)\s*=(?!=)\s*(?:Newtonsoft\.Json\.)?TypeNameHandling\.(?<value>All|Auto|Objects|Arrays)\b")]
    private static partial Regex TypeNameHandlingEnabled();

    [GeneratedRegex(@"\bSerializationBinder\s*=(?!=)")]
    private static partial Regex SerializationBinderAssignment();
}
