namespace AspireForge.Generators.Docker;

internal static class DockerAssets
{
    public static string Dockerfile(string apiProjectName) => $"""
        FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
        WORKDIR /src

        COPY . .
        RUN dotnet restore
        RUN dotnet publish "src/{apiProjectName}/{apiProjectName}.csproj" -c Release -o /app --no-restore

        FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
        WORKDIR /app
        COPY --from=build /app .

        ENTRYPOINT ["dotnet", "{apiProjectName}.dll"]
        """;

    public static string DockerIgnore() => """
        **/bin/
        **/obj/
        **/.vs/
        **/.git/
        **/.github/
        **/Dockerfile
        **/.dockerignore
        **/*.md
        """;

    public static string DockerCompose(string projectName, bool includePostgres, bool includeSqlServer, bool includeRedis)
    {
        var lines = new List<string>
        {
            "services:",
            "  api:",
            "    build:",
            "      context: .",
            "      dockerfile: Dockerfile",
            "    ports:",
            "      - \"8080:8080\"",
        };

        if (includePostgres || includeSqlServer || includeRedis)
        {
            lines.Add("    depends_on:");
            if (includePostgres)
            {
                lines.Add("      - postgres");
            }
            if (includeSqlServer)
            {
                lines.Add("      - sqlserver");
            }
            if (includeRedis)
            {
                lines.Add("      - redis");
            }
        }

        if (includePostgres)
        {
            lines.AddRange(
            [
                string.Empty,
                "  postgres:",
                "    image: postgres:16",
                "    environment:",
                $"      POSTGRES_DB: {projectName}",
                "      POSTGRES_USER: postgres",
                "      POSTGRES_PASSWORD: postgres",
                "    ports:",
                "      - \"5432:5432\"",
                "    volumes:",
                "      - postgres-data:/var/lib/postgresql/data",
            ]);
        }

        if (includeSqlServer)
        {
            lines.AddRange(
            [
                string.Empty,
                "  sqlserver:",
                "    image: mcr.microsoft.com/mssql/server:2022-latest",
                "    environment:",
                "      ACCEPT_EULA: \"Y\"",
                "      MSSQL_SA_PASSWORD: Your_password123",
                "    ports:",
                "      - \"1433:1433\"",
                "    volumes:",
                "      - sqlserver-data:/var/opt/mssql",
            ]);
        }

        if (includeRedis)
        {
            lines.AddRange(
            [
                string.Empty,
                "  redis:",
                "    image: redis:7",
                "    ports:",
                "      - \"6379:6379\"",
            ]);
        }

        if (includePostgres || includeSqlServer)
        {
            lines.AddRange([string.Empty, "volumes:"]);

            if (includePostgres)
            {
                lines.Add("  postgres-data:");
            }

            if (includeSqlServer)
            {
                lines.Add("  sqlserver-data:");
            }
        }

        return string.Join('\n', lines) + "\n";
    }
}
