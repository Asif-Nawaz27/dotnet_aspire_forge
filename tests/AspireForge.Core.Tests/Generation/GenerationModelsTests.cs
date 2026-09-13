using AspireForge.Core.Generation;

namespace AspireForge.Core.Tests.Generation;

public class GenerationModelsTests
{
    [Fact]
    public void GenerationOptions_DefaultsToEmptyParametersAndNoOverwrite()
    {
        var options = new GenerationOptions
        {
            ProjectName = "Sample",
            Architecture = "clean",
            OutputPath = "/tmp",
        };

        Assert.False(options.Overwrite);
        Assert.Empty(options.Parameters);
    }

    [Fact]
    public void GenerationResult_DefaultsToEmptyFilesAndErrors()
    {
        var result = new GenerationResult { Success = true };

        Assert.Empty(result.GeneratedFiles);
        Assert.Empty(result.Errors);
    }
}
