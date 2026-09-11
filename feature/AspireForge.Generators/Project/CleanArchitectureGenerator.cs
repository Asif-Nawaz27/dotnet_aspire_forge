using AspireForge.Core.Generation;
using AspireForge.Generators.Docker;
using AspireForge.Generators.GitHub;

namespace AspireForge.Generators.Project;

public sealed class CleanArchitectureGenerator : IProjectGenerator
{
    public string Name => "clean";

    public GenerationResult Generate(GenerationOptions options)
    {
        var generatedFiles = new List<string>();

        try
        {
            var root = Path.Combine(options.OutputPath, options.ProjectName);

            if (Directory.Exists(root) && !options.Overwrite)
            {
                return new GenerationResult
                {
                    Success = false,
                    Errors = [$"Directory '{root}' already exists. Remove it or choose a different name."],
                };
            }

            Directory.CreateDirectory(root);

            var srcDirectory = Path.Combine(root, "src");
            var testsDirectory = Path.Combine(root, "tests");

            var domainName = $"{options.ProjectName}.Domain";
            var applicationName = $"{options.ProjectName}.Application";
            var infrastructureName = $"{options.ProjectName}.Infrastructure";
            var apiName = $"{options.ProjectName}.Api";
            var integrationTestsName = $"{options.ProjectName}.IntegrationTests";

            var domainPath = Path.Combine(srcDirectory, domainName);
            var applicationPath = Path.Combine(srcDirectory, applicationName);
            var infrastructurePath = Path.Combine(srcDirectory, infrastructureName);
            var apiPath = Path.Combine(srcDirectory, apiName);
            var integrationTestsPath = Path.Combine(testsDirectory, integrationTestsName);

            DotnetCli.Run(["new", "sln", "-n", options.ProjectName, "-o", root], root);
            DotnetCli.Run(["new", "classlib", "-n", domainName, "-o", domainPath], root);
            DotnetCli.Run(["new", "classlib", "-n", applicationName, "-o", applicationPath], root);
            DotnetCli.Run(["new", "classlib", "-n", infrastructureName, "-o", infrastructurePath], root);
            DotnetCli.Run(["new", "webapi", "-n", apiName, "-o", apiPath], root);
            DotnetCli.Run(["new", "xunit", "-n", integrationTestsName, "-o", integrationTestsPath], root);

            foreach (var projectPath in new[] { domainPath, applicationPath, infrastructurePath, apiPath, integrationTestsPath })
            {
                DotnetCli.Run(["sln", "add", projectPath], root);
            }

            DotnetCli.Run(["add", applicationPath, "reference", domainPath], root);
            DotnetCli.Run(["add", infrastructurePath, "reference", domainPath, applicationPath], root);
            DotnetCli.Run(["add", apiPath, "reference", applicationPath, infrastructurePath], root);
            DotnetCli.Run(["add", integrationTestsPath, "reference", apiPath], root);

            RemoveTemplatePlaceholder(domainPath);
            RemoveTemplatePlaceholder(applicationPath);
            RemoveTemplatePlaceholder(infrastructurePath);

            var solutionFile = Directory.EnumerateFiles(root, "*.sln*", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (solutionFile is not null)
            {
                generatedFiles.Add(Path.GetFileName(solutionFile));
            }
            generatedFiles.Add($"src/{domainName}/");
            generatedFiles.Add($"src/{applicationName}/");
            generatedFiles.Add($"src/{infrastructureName}/");
            generatedFiles.Add($"src/{apiName}/");
            generatedFiles.Add($"tests/{integrationTestsName}/");

            generatedFiles.Add(WriteFile(root, "Dockerfile", DockerAssets.Dockerfile(apiName)));
            generatedFiles.Add(WriteFile(root, ".dockerignore", DockerAssets.DockerIgnore()));
            generatedFiles.Add(WriteFile(root, ".gitignore", GitIgnoreTemplate.Render()));
            generatedFiles.Add(WriteFile(root, "README.md", ReadmeTemplate.Render(options.ProjectName)));
            generatedFiles.Add(WriteFile(
                root, Path.Combine(".github", "workflows", "ci.yml"), GitHubWorkflows.Ci(options.ProjectName)));

            return new GenerationResult { Success = true, GeneratedFiles = generatedFiles };
        }
        catch (Exception ex)
        {
            return new GenerationResult
            {
                Success = false,
                GeneratedFiles = generatedFiles,
                Errors = [ex.Message],
            };
        }
    }

    private static void RemoveTemplatePlaceholder(string projectPath)
    {
        var placeholder = Path.Combine(projectPath, "Class1.cs");

        if (File.Exists(placeholder))
        {
            File.Delete(placeholder);
        }
    }

    private static string WriteFile(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return relativePath.Replace('\\', '/');
    }
}
