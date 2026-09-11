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
}
