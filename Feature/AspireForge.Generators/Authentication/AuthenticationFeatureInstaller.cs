using AspireForge.Core.Features;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Authentication;

// Wires up ASP.NET Core's own authentication/authorization middleware (JWT Bearer) rather than a
// bespoke auth framework. ASP.NET Core Identity, Entra ID, and Keycloak are later, separate features
// built on top of this same infrastructure - not alternatives to it.
public sealed class AuthenticationFeatureInstaller : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = "authentication",
        Name = "Authentication",
        Description = "Adds JWT Bearer authentication and authorization using ASP.NET Core's own middleware.",
    };

    public Task<IReadOnlyList<string>> InstallAsync(
        FeatureContext context, CancellationToken cancellationToken = default)
    {
        var steps = new List<string>();

        var apiCsproj = CleanArchitectureLayout.ApiCsproj(context);
        var apiProjectPath = CleanArchitectureLayout.ApiProjectPath(context);
        var programCsPath = Path.Combine(apiProjectPath, "Program.cs");

        DotnetCli.Run(["add", apiCsproj, "package", "Microsoft.AspNetCore.Authentication.JwtBearer"], context.RootPath);
        steps.Add("Added JWT Bearer authentication");

        // appsettings.json ships with every build, so it holds no key (SEC004); each environment supplies
        // one - user-secrets locally, Jwt__SigningKey in production. Only the development file gets a
        // placeholder, so `dotnet run` works out of the box.
        AppSettingsEditor.AddSection(
            Path.Combine(apiProjectPath, "appsettings.json"),
            "Jwt",
            new Dictionary<string, string>
            {
                ["Issuer"] = context.ProjectName,
                ["Audience"] = context.ProjectName,
                ["SigningKey"] = string.Empty,
            });

        var developmentSettings = Path.Combine(apiProjectPath, "appsettings.Development.json");

        if (File.Exists(developmentSettings))
        {
            AppSettingsEditor.AddSection(
                developmentSettings,
                "Jwt",
                new Dictionary<string, string>
                {
                    ["SigningKey"] = "CHANGE_ME_DEVELOPMENT_ONLY_JWT_SIGNING_KEY_32_CHARS_MIN",
                });
        }

        steps.Add("Added JWT configuration (signing key: development placeholder only - set Jwt__SigningKey per environment)");

        ProgramFileEditor.AddUsingsAndServiceRegistration(
            programCsPath,
            usings:
            [
                "System.Text",
                "Microsoft.AspNetCore.Authentication.JwtBearer",
                "Microsoft.IdentityModel.Tokens",
            ],
            serviceRegistrationLines:
            [
                "builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)",
                "    .AddJwtBearer(options =>",
                "    {",
                "        options.TokenValidationParameters = new TokenValidationParameters",
                "        {",
                "            ValidateIssuer = true,",
                "            ValidateAudience = true,",
                "            ValidateLifetime = true,",
                "            ValidateIssuerSigningKey = true,",
                "            ValidIssuer = builder.Configuration[\"Jwt:Issuer\"],",
                "            ValidAudience = builder.Configuration[\"Jwt:Audience\"],",
                "            IssuerSigningKey = new SymmetricSecurityKey(",
                "                Encoding.UTF8.GetBytes(builder.Configuration[\"Jwt:SigningKey\"]!)),",
                "        };",
                "    });",
                string.Empty,
                "builder.Services.AddAuthorization();",
            ]);
        steps.Add("Added authentication services");

        // After the exception handler and before the rate limiter, the order SEC009 checks.
        ProgramFileEditor.InsertBeforeOrAfterBuilderBuild(
            programCsPath,
            "app.UseRateLimiter(",
            ["app.UseAuthentication();", "app.UseAuthorization();"]);
        steps.Add("Added authentication middleware");

        ProgramFileEditor.InsertBeforeAppRun(
            programCsPath,
            ["app.MapGet(\"/secure\", () => \"You are authenticated.\").RequireAuthorization();"]);
        steps.Add("Added an example protected endpoint");

        return Task.FromResult<IReadOnlyList<string>>(steps);
    }
}
