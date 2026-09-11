namespace AspireForge.Generators.GitHub;

internal static class GitHubWorkflows
{
    public static string Ci(string projectName) => $"""
        name: CI

        on:
          push:
            branches: [ main ]
          pull_request:
            branches: [ main ]

        jobs:
          build:
            runs-on: ubuntu-latest
            steps:
              - uses: actions/checkout@v4

              - name: Setup .NET
                uses: actions/setup-dotnet@v4
                with:
                  dotnet-version: '10.0.x'

              - name: Restore
                run: dotnet restore {projectName}.sln

              - name: Build
                run: dotnet build {projectName}.sln --no-restore --configuration Release

              - name: Test
                run: dotnet test {projectName}.sln --no-build --configuration Release
        """;
}
