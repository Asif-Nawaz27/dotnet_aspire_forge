namespace AspireForge.Generators.Project;

internal static class ReadmeTemplate
{
    public static string Render(string projectName) => $"""
        # {projectName}

        Generated with AspireForge using the `clean` architecture template.

        ## Layout

        - `src/{projectName}.Domain` - entities and business rules with no outward dependencies.
        - `src/{projectName}.Application` - use cases and abstractions, depends only on Domain.
        - `src/{projectName}.Infrastructure` - implementations of Application abstractions (persistence, external services).
        - `src/{projectName}.Api` - the ASP.NET Core host and composition root.
        - `tests/{projectName}.IntegrationTests` - end-to-end tests against the Api.

        ## Getting started

        ```bash
        dotnet restore
        dotnet build
        dotnet test
        dotnet run --project src/{projectName}.Api
        ```

        Run `aspireforge doctor src/{projectName}.Api` to check the generated API against AspireForge's rule set.
        """;
}
