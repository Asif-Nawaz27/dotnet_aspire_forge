using App.Domain.GitHub;

namespace App.UnitTests.Domain;

public class GitHubUsernameTests
{
    [Theory]
    [InlineData("octocat")]
    [InlineData("a")]
    [InlineData("some-user")]
    [InlineData("User123")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789abc")] // 39 characters, the maximum
    public void IsValid_AcceptsGitHubUsernames(string username) =>
        Assert.True(GitHubUsername.IsValid(username));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("under_score")]
    [InlineData("../etc/passwd")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789abcd")] // 40 characters
    public void IsValid_RejectsAnythingElse(string? username) =>
        Assert.False(GitHubUsername.IsValid(username));
}
