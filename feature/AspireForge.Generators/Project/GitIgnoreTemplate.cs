namespace AspireForge.Generators.Project;

internal static class GitIgnoreTemplate
{
    public static string Render() => """
        bin/
        obj/
        .vs/
        *.user
        *.suo
        .idea/
        """;
}
