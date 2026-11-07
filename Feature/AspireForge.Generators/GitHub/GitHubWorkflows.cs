namespace AspireForge.Generators.GitHub;

internal static class GitHubWorkflows
{
    // doctor runs three times because each run has a different job, and it only takes seconds:
    //   - the job summary and SARIF steps never fail the build (`|| true`), so both are written even
    //     when doctor finds errors
    //   - the last step is the actual check, and prints the text report to the job log, so a failed
    //     build explains itself there
    // The upload is allowed to fail: code scanning needs GitHub Advanced Security on a private
    // repository, and pull requests from forks can't write results. Neither should fail the build.
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
            permissions:
              contents: read
              security-events: write
            steps:
              - uses: actions/checkout@v4

              - name: Setup .NET
                uses: actions/setup-dotnet@v4
                with:
                  dotnet-version: '10.0.x'

              - name: Restore
                run: dotnet restore {projectName}.slnx

              - name: Build
                run: dotnet build {projectName}.slnx --no-restore --configuration Release

              - name: Test
                run: dotnet test {projectName}.slnx --no-build --configuration Release

              - name: Install AspireForge
                run: |
                  dotnet tool install --global AspireForge --version {ToolVersion.Current}
                  echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"

              - name: Doctor summary
                run: aspireforge doctor src/{projectName}.Api --format markdown >> "$GITHUB_STEP_SUMMARY" || true

              - name: Doctor SARIF
                run: aspireforge doctor src/{projectName}.Api --format sarif > doctor.sarif || true

              - name: Upload to code scanning
                uses: github/codeql-action/upload-sarif@v4
                continue-on-error: true
                with:
                  sarif_file: doctor.sarif

              - name: Doctor
                run: aspireforge doctor src/{projectName}.Api
        """;
}
