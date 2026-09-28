using App.Domain.GitHub;

namespace App.UnitTests.Domain;

public class FollowRelationshipReportTests
{
    [Fact]
    public void Create_SplitsBothDirectionsOfTheRelationship()
    {
        var report = FollowRelationshipReport.Create(
            "someuser",
            followers: Users("alice", "bob"),
            following: Users("bob", "carol", "dave"));

        Assert.Equal(["carol", "dave"], report.NotFollowingBack.Select(user => user.Login));
        Assert.Equal(["alice"], report.NotFollowedBack.Select(user => user.Login));
        Assert.Equal(2, report.FollowersCount);
        Assert.Equal(3, report.FollowingCount);
    }

    [Fact]
    public void Create_ComparesLoginsCaseInsensitively()
    {
        var report = FollowRelationshipReport.Create("someuser", Users("Alice"), Users("alice"));

        Assert.Empty(report.NotFollowingBack);
        Assert.Empty(report.NotFollowedBack);
    }

    [Fact]
    public void Create_ReturnsEmptyLists_WhenEveryRelationshipIsMutual()
    {
        var report = FollowRelationshipReport.Create("someuser", Users("alice", "bob"), Users("bob", "alice"));

        Assert.Empty(report.NotFollowingBack);
        Assert.Empty(report.NotFollowedBack);
    }

    internal static List<GitHubUser> Users(params string[] logins) =>
        logins.Select((login, index) => new GitHubUser(index + 1, login, "avatar", "html")).ToList();
}
