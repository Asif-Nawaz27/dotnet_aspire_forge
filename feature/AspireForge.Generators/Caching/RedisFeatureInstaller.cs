using AspireForge.Core.Features;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Caching;

public sealed class RedisFeatureInstaller : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = "redis",
        Name = "Redis",
        Description = "Adds the StackExchange.Redis distributed cache client and wires it into the Api.",
    };

    public Task<IReadOnlyList<string>> InstallAsync(
        FeatureContext context, CancellationToken cancellationToken = default)
    {
        var steps = new List<string>();

        var infrastructureCsproj = CleanArchitectureLayout.InfrastructureCsproj(context);
        var apiProjectPath = CleanArchitectureLayout.ApiProjectPath(context);

        DotnetCli.Run(
            ["add", infrastructureCsproj, "package", "Microsoft.Extensions.Caching.StackExchangeRedis"],
            context.RootPath);
        steps.Add("Added StackExchange.Redis client");

        AppSettingsEditor.AddConnectionString(
            Path.Combine(apiProjectPath, "appsettings.json"), "Redis", "localhost:6379");
        steps.Add("Added connection configuration");

        ProgramFileEditor.AddUsingsAndServiceRegistration(
            Path.Combine(apiProjectPath, "Program.cs"),
            usings: [],
            serviceRegistrationLines:
            [
                "builder.Services.AddStackExchangeRedisCache(options =>",
                "    options.Configuration = builder.Configuration.GetConnectionString(\"Redis\"));",
            ]);
        steps.Add("Added distributed cache registration");

        return Task.FromResult<IReadOnlyList<string>>(steps);
    }
}
