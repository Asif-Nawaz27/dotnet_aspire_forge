using AspireForge.Core.Features;
using AspireForge.Core.Generation;
using AspireForge.Generators.Docker;
using AspireForge.Generators.GitHub;

namespace AspireForge.Generators.Project;

// Composes existing dotnet new templates (classlib/webapi/xunit) rather than a bespoke template engine.
public sealed class CleanArchitectureGenerator : IProjectGenerator
{
    private static readonly IFeatureInstaller[] Features =
    [
        new ReadmeFeatureInstaller(),
        new GitIgnoreFeatureInstaller(),
        new DockerFeatureInstaller(),
        new GitHubActionsFeatureInstaller(),
    ];

    public string Name => "clean";

    public GenerationResult Generate(GenerationOptions options)
    {
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

            var templates = SelectTemplates(options, root);

            GenerateProjects(options, root, templates);
            var featureSteps = ApplyFeatures(options, root);

            var generatedFiles = DescribeGeneratedProjects(root, templates)
                .Concat(featureSteps)
                .ToList();

            return new GenerationResult { Success = true, GeneratedFiles = generatedFiles };
        }
        catch (Exception ex)
        {
            return new GenerationResult { Success = false, Errors = [ex.Message] };
        }
    }

    // select template: map each architectural layer to an installed .NET template.
    private static IReadOnlyList<ProjectTemplate> SelectTemplates(GenerationOptions options, string root)
    {
        var srcDirectory = Path.Combine(root, "src");
        var testsDirectory = Path.Combine(root, "tests");

        string ProjectName(string layer) => $"{options.ProjectName}.{layer}";

        return
        [
            new ProjectTemplate("Domain", "classlib", ProjectName("Domain"), Path.Combine(srcDirectory, ProjectName("Domain"))),
            new ProjectTemplate("Application", "classlib", ProjectName("Application"), Path.Combine(srcDirectory, ProjectName("Application"))),
            new ProjectTemplate("Infrastructure", "classlib", ProjectName("Infrastructure"), Path.Combine(srcDirectory, ProjectName("Infrastructure"))),
            new ProjectTemplate("Api", "webapi", ProjectName("Api"), Path.Combine(srcDirectory, ProjectName("Api"))),
            new ProjectTemplate("IntegrationTests", "xunit", ProjectName("IntegrationTests"), Path.Combine(testsDirectory, ProjectName("IntegrationTests"))),
        ];
    }

    // configure + generate: invoke each selected template, then wire the solution together.
    private static void GenerateProjects(GenerationOptions options, string root, IReadOnlyList<ProjectTemplate> templates)
    {
        DotnetCli.Run(["new", "sln", "-n", options.ProjectName, "-o", root], root);

        foreach (var template in templates)
        {
            DotnetCli.Run(ConfigureTemplateArguments(template, options), root);
            DotnetCli.Run(["sln", "add", template.ProjectPath], root);
        }

        LinkArchitectureReferences(templates, root);
        RemoveTemplatePlaceholders(templates);
    }

    private static List<string> ConfigureTemplateArguments(ProjectTemplate template, GenerationOptions options)
    {
        List<string> arguments = ["new", template.TemplateName, "-n", template.ProjectName, "-o", template.ProjectPath];

        if (options.Parameters.TryGetValue($"{template.Layer}:args", out var extraArguments))
        {
            arguments.AddRange(extraArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        return arguments;
    }

    private static void LinkArchitectureReferences(IReadOnlyList<ProjectTemplate> templates, string root)
    {
        string PathOf(string layer) => templates.First(template => template.Layer == layer).ProjectPath;

        DotnetCli.Run(["add", PathOf("Application"), "reference", PathOf("Domain")], root);
        DotnetCli.Run(["add", PathOf("Infrastructure"), "reference", PathOf("Domain"), PathOf("Application")], root);
        DotnetCli.Run(["add", PathOf("Api"), "reference", PathOf("Application"), PathOf("Infrastructure")], root);
        DotnetCli.Run(["add", PathOf("IntegrationTests"), "reference", PathOf("Api")], root);
    }

    private static void RemoveTemplatePlaceholders(IReadOnlyList<ProjectTemplate> templates)
    {
        foreach (var template in templates.Where(template => template.TemplateName == "classlib"))
        {
            var placeholder = Path.Combine(template.ProjectPath, "Class1.cs");

            if (File.Exists(placeholder))
            {
                File.Delete(placeholder);
            }
        }
    }

    // apply features: layer AspireForge's own additions (Docker, CI, docs) on top of the templates.
    private static IReadOnlyList<string> ApplyFeatures(GenerationOptions options, string root)
    {
        var context = new FeatureContext { ProjectName = options.ProjectName, RootPath = root };
        var steps = new List<string>();

        foreach (var feature in Features)
        {
            steps.AddRange(feature.InstallAsync(context).GetAwaiter().GetResult());
        }

        return steps;
    }

    private static IEnumerable<string> DescribeGeneratedProjects(string root, IReadOnlyList<ProjectTemplate> templates)
    {
        var solutionFile = Directory.EnumerateFiles(root, "*.sln*", SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (solutionFile is not null)
        {
            yield return Path.GetFileName(solutionFile);
        }

        foreach (var template in templates)
        {
            yield return $"{Path.GetRelativePath(root, template.ProjectPath).Replace('\\', '/')}/";
        }
    }

}

internal sealed record ProjectTemplate(string Layer, string TemplateName, string ProjectName, string ProjectPath);
