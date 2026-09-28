using App.Application.GitHub;
using Microsoft.Extensions.DependencyInjection;

namespace App.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<GitHubUserServiceOptions>()
            .BindConfiguration(GitHubUserServiceOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHybridCache();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IGitHubUserService, GitHubUserService>();

        return services;
    }
}
