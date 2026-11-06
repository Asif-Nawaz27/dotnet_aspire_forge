using System.Xml.Linq;
using AspireForge.Analyzers.Suppressions;

namespace AspireForge.Analyzers.Tests.Suppressions;

public class InlineSuppressionsTests
{
    [Fact]
    public void NextLine_AppliesToTheLineBelowTheComment()
    {
        var suppressions = InlineSuppressions.Parse(["// aspireforge-disable-next-line SEC002", "code();"]);

        Assert.Equal([2], suppressions.Keys);
    }

    [Fact]
    public void Line_AppliesToItsOwnLine()
    {
        var suppressions = InlineSuppressions.Parse(["first();", "code(); // aspireforge-disable-line SEC002"]);

        Assert.Equal([2], suppressions.Keys);
    }

    [Theory]
    [InlineData("// aspireforge-disable-line SEC002: a reason")]
    [InlineData("# aspireforge-disable-line SEC002: a reason")]
    [InlineData("<!-- aspireforge-disable-line SEC002: a reason -->")]
    public void Reads_EveryCommentStyle(string line)
    {
        var suppression = Assert.Single(InlineSuppressions.Parse([line])[1]);

        Assert.Equal(["SEC002"], suppression.RuleIds);
        Assert.Equal("a reason", suppression.Reason);
    }

    [Fact]
    public void Reads_SeveralRules_InAnyCase()
    {
        var suppression = Assert.Single(InlineSuppressions.Parse(["// aspireforge-disable-line sec002, OBS005"])[1]);

        Assert.Equal(["SEC002", "OBS005"], suppression.RuleIds.Order().Reverse());
    }

    [Fact]
    public void Reason_IsOptional_AndMayContainColons()
    {
        var parsed = InlineSuppressions.Parse(
        [
            "// aspireforge-disable-line SEC002",
            "// aspireforge-disable-line SEC002: see https://example.com/adr: 12",
        ]);

        Assert.Null(Assert.Single(parsed[1]).Reason);
        Assert.Equal("see https://example.com/adr: 12", Assert.Single(parsed[2]).Reason);
    }

    // A comment naming no rule would silence everything on the line.
    [Theory]
    [InlineData("// aspireforge-disable-line")]
    [InlineData("// aspireforge-disable-next-line")]
    [InlineData("// aspireforge-disable-line: no rule named")]
    [InlineData("// aspireforge-disable-line SEC002 forgot the colon")]
    [InlineData("// nothing to see here")]
    public void Ignores_CommentsThatDontNameRulesProperly(string line) =>
        Assert.Empty(InlineSuppressions.Parse([line]));

    // Why the reason separator isn't "--": this would throw if the syntax put "--" inside the comment.
    [Fact]
    public void TheDocumentedProjectFileComment_IsValidXml()
    {
        const string Comment = "<!-- aspireforge-disable-next-line DEP004: preview needed for the new API -->";

        var document = XDocument.Parse($"<Project>{Comment}</Project>");

        Assert.NotNull(document.Root);
        Assert.Single(InlineSuppressions.Parse([Comment]));
    }
}
