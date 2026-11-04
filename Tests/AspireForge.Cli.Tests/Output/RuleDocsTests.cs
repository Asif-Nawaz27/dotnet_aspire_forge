using System.Text.RegularExpressions;
using AspireForge.Analyzers;
using AspireForge.Cli.Commands;
using AspireForge.Cli.Output;

namespace AspireForge.Cli.Tests.Output;

public class RuleDocsTests
{
    [Theory]
    [InlineData("SEC006 - HSTS missing", "sec006---hsts-missing")]
    [InlineData("REL004 - Health checks don't check dependencies", "rel004---health-checks-dont-check-dependencies")]
    [InlineData("DEP001 - Unsupported .NET version", "dep001---unsupported-net-version")]
    [InlineData("REL008 - async void method", "rel008---async-void-method")]
    public void Anchor_MatchesGitHubHeadingSlugs(string heading, string anchor) =>
        Assert.Equal(anchor, RuleDocs.Anchor(heading));

    // doctor prints a docs link for every finding; a renamed rule or heading must fail here rather than
    // ship a link that lands at the top of the page.
    [Fact]
    public void EveryRule_LinksToAnExistingHeading()
    {
        var docs = FindDocsDirectory();
        var titles = AnalysisRuleSet.CreateDefault(includeVulnerabilityScan: true).ToDictionary(rule => rule.Id, rule => rule.Title);
        var missing = new List<string>();

        foreach (var entry in RuleCatalog.Entries)
        {
            var url = RuleDocs.Url(entry.Category, entry.RuleId, titles[entry.RuleId]);
            var file = Path.Combine(docs, $"{entry.Category.ToLowerInvariant()}.md");
            var anchors = File.Exists(file)
                ? File.ReadLines(file)
                    .Select(line => Regex.Match(line, @"^##\s+(.+)$"))
                    .Where(match => match.Success)
                    .Select(match => RuleDocs.Anchor(match.Groups[1].Value))
                    .ToHashSet()
                : [];

            if (!anchors.Contains(url[(url.IndexOf('#') + 1)..]))
            {
                missing.Add($"{entry.RuleId} -> {url}");
            }
        }

        Assert.True(missing.Count == 0, "Docs links without a matching heading:\n" + string.Join("\n", missing));
    }

    private static string FindDocsDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var docs = Path.Combine(directory.FullName, "docs", "rules");

            if (Directory.Exists(docs))
            {
                return docs;
            }
        }

        throw new DirectoryNotFoundException("docs/rules not found above the test output directory.");
    }
}
