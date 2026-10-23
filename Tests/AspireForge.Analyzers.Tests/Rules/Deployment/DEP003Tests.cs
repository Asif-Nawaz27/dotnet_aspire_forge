using AspireForge.Analyzers.Rules.Deployment;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Deployment;

public class DEP003Tests : IDisposable
{
    // The App's own Dockerfile: multi-stage, pinned, non-root, with a .dockerignore.
    private const string GoodDockerfile = """
        # syntax=docker/dockerfile:1
        FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
        WORKDIR /src
        COPY . .
        RUN dotnet publish Api/Api.csproj -c Release -o /app

        FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
        WORKDIR /app
        COPY --from=build /app .
        USER $APP_UID
        ENTRYPOINT ["dotnet", "Api.dll"]
        """;

    private readonly string _repository = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));
    private readonly DEP003ContainerProblems _rule = new();

    public DEP003Tests()
    {
        // Marks the repository root, so the upward search never wanders into the temp directory.
        Directory.CreateDirectory(Path.Combine(_repository, ".git"));
        Directory.CreateDirectory(ApiDirectory);
    }

    private string ApiDirectory => Path.Combine(_repository, "src", "Api");

    [Fact]
    public async Task DoesNotFire_ForAWellFormedMultiStageDockerfile()
    {
        Write("Dockerfile", GoodDockerfile);
        Write(".dockerignore", "**/bin/\n**/obj/\n.git/");

        var issue = await Evaluate();

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenThereIsNoDockerfile()
    {
        var issue = await Evaluate();

        Assert.Null(issue);
    }

    [Fact]
    public async Task Fires_WhenTheFinalStageHasNoUser_AtTheFinalFromLine()
    {
        Write("Dockerfile", GoodDockerfile.Replace("USER $APP_UID\n", string.Empty));
        Write(".dockerignore", "**/bin/");

        var issue = await Evaluate();

        Assert.NotNull(issue);
        Assert.Equal("DEP003", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Equal(7, issue.LineNumber);
        Assert.EndsWith("Dockerfile", issue.FilePath);
        Assert.Contains("no USER instruction, so the container runs as root", issue.Description);
    }

    [Theory]
    [InlineData("USER root")]
    [InlineData("USER 0")]
    [InlineData("USER root:root")]
    public async Task Fires_WhenTheFinalStageSwitchesToRoot(string user)
    {
        Write("Dockerfile", GoodDockerfile.Replace("USER $APP_UID", user));
        Write(".dockerignore", "**/bin/");

        var issue = await Evaluate();

        Assert.Equal(10, issue!.LineNumber);
        Assert.Contains("the container runs as root", issue.Description);
    }

    [Fact]
    public async Task OnlyLooksAtUserInTheFinalStage()
    {
        Write("Dockerfile", """
            FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
            USER app
            RUN dotnet publish -o /app
            FROM mcr.microsoft.com/dotnet/aspnet:10.0
            COPY --from=build /app .
            """);

        var issue = await Evaluate();

        Assert.Contains("no USER instruction", issue!.Description);
    }

    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled")]
    [InlineData("mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra")]
    public async Task DoesNotRequireUser_ForChiseledImages(string image)
    {
        Write("Dockerfile", $"FROM {image}\nCOPY app/ /app\nENTRYPOINT [\"/app/Api\"]");

        var issue = await Evaluate();

        Assert.Null(issue);
    }

    [Fact]
    public async Task Fires_WhenTheFinalStageIsTheSdkImage()
    {
        Write("Dockerfile", """
            FROM mcr.microsoft.com/dotnet/sdk:10.0
            WORKDIR /src
            COPY Api/ Api/
            RUN dotnet publish Api/Api.csproj -o /app
            USER $APP_UID
            ENTRYPOINT ["dotnet", "/app/Api.dll"]
            """);

        var issue = await Evaluate();

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Contains("the final stage is the .NET SDK image", issue.Description);
    }

    [Theory]
    [InlineData("FROM mcr.microsoft.com/dotnet/aspnet")]
    [InlineData("FROM mcr.microsoft.com/dotnet/aspnet:latest")]
    [InlineData("FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:LATEST AS runtime")]
    [InlineData("FROM localhost:5000/base")]
    public async Task Suggests_PinningUnversionedBaseImages(string from)
    {
        Write("Dockerfile", $"{from}\nUSER $APP_UID");

        var issue = await Evaluate();

        Assert.Equal(Severity.Suggestion, issue!.Severity);
        Assert.Contains("isn't pinned to a version", issue.Description);
    }

    [Theory]
    [InlineData("FROM mcr.microsoft.com/dotnet/aspnet:10.0")]
    [InlineData("FROM mcr.microsoft.com/dotnet/aspnet@sha256:0123456789abcdef")]
    [InlineData("FROM localhost:5000/base:1.2")]
    [InlineData("FROM ${BASE_IMAGE}")]
    [InlineData("FROM scratch")]
    public async Task DoesNotFlag_PinnedDigestArgOrScratchImages(string from)
    {
        Write("Dockerfile", $"{from}\nUSER app");

        var issue = await Evaluate();

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFlag_FromAnEarlierStage()
    {
        Write("Dockerfile", """
            FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
            FROM base AS final
            USER $APP_UID
            """);

        var issue = await Evaluate();

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("COPY . .")]
    [InlineData("COPY --chown=app:app . /src")]
    [InlineData("ADD . /app")]
    public async Task Suggests_ADockerignore_WhenTheWholeContextIsCopied(string copy)
    {
        Write("Dockerfile", GoodDockerfile.Replace("COPY . .", copy));

        var issue = await Evaluate();

        Assert.Equal(Severity.Suggestion, issue!.Severity);
        Assert.Contains("no .dockerignore", issue.Description);
    }

    [Fact]
    public async Task DoesNotSuggestADockerignore_WhenOnlySpecificFilesAreCopied()
    {
        Write("Dockerfile", GoodDockerfile.Replace("COPY . .", "COPY Api/Api.csproj Api/"));

        var issue = await Evaluate();

        Assert.Null(issue);
    }

    [Fact]
    public async Task FindsASolutionLevelDockerfile_AboveTheProject()
    {
        File.WriteAllText(Path.Combine(_repository, "Dockerfile"), "FROM mcr.microsoft.com/dotnet/aspnet:10.0\n");

        var issue = await Evaluate();

        Assert.Equal(Path.Combine(_repository, "Dockerfile"), issue!.FilePath);
    }

    [Fact]
    public async Task PrefersTheNearestDockerfile()
    {
        File.WriteAllText(Path.Combine(_repository, "Dockerfile"), "FROM mcr.microsoft.com/dotnet/aspnet:10.0\n");
        Write("Api.Dockerfile", "FROM mcr.microsoft.com/dotnet/aspnet:10.0\nUSER $APP_UID\n");

        var issue = await Evaluate();

        Assert.Null(issue);
    }

    [Fact]
    public async Task HandlesLineContinuations_AndReportsTheStartingLine()
    {
        Write("Dockerfile", """
            FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
            RUN dotnet restore && \
                dotnet publish -o /app
            FROM \
                mcr.microsoft.com/dotnet/aspnet:10.0
            COPY --from=build /app .
            """);

        var issue = await Evaluate();

        Assert.Equal(4, issue!.LineNumber);
    }

    [Fact]
    public async Task ReportsEveryProblem_MostSevereFirst()
    {
        Write("Dockerfile", "FROM mcr.microsoft.com/dotnet/sdk:latest\nCOPY . .\nRUN dotnet publish -o /app\n");

        var issue = await Evaluate();

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Contains("the final stage is the .NET SDK image", issue.Description);
        Assert.Contains("no USER instruction", issue.Description);
        Assert.Contains("isn't pinned", issue.Description);
        Assert.Contains("no .dockerignore", issue.Description);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repository))
        {
            Directory.Delete(_repository, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private void Write(string fileName, string content) =>
        File.WriteAllText(Path.Combine(ApiDirectory, fileName), content);

    private Task<AnalysisIssue?> Evaluate() =>
        _rule.EvaluateAsync(new ProjectContext
        {
            RootDirectory = ApiDirectory,
            ProjectFile = Path.Combine(ApiDirectory, "Api.csproj"),
            ProjectName = "Api",
            SourceFiles = [],
            ProjectReferences = [],
            PackageReferences = [],
        });
}
