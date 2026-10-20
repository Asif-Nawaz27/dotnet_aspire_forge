using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC012Tests
{
    private readonly SEC012UnsafeDeserialization _rule = new();

    [Fact]
    public async Task Fires_ForBinaryFormatter_WithFileAndLine()
    {
        using var project = TestProject.Create("""
            public object Load(Stream stream)
            {
                var formatter = new BinaryFormatter();
                return formatter.Deserialize(stream);
            }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC012", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains("BinaryFormatter lets the payload choose which type to create", issue.Description);
    }

    [Theory]
    [InlineData("var f = new SoapFormatter();")]
    [InlineData("var s = new NetDataContractSerializer();")]
    [InlineData("var f = new LosFormatter();")]
    [InlineData("var f = new ObjectStateFormatter();")]
    [InlineData("IFormatter f = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();")]
    [InlineData("var serializer = new JavaScriptSerializer(new SimpleTypeResolver());")]
    public async Task Fires_ForOtherInsecureSerializers(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
    }

    [Theory]
    [InlineData("All")]
    [InlineData("Auto")]
    [InlineData("Objects")]
    [InlineData("Arrays")]
    public async Task Fires_ForEveryTypeNameHandlingExceptNone(string value)
    {
        using var project = TestProject.Create(
            $"var settings = new JsonSerializerSettings {{ TypeNameHandling = TypeNameHandling.{value} }};");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Contains($"TypeNameHandling = TypeNameHandling.{value}", issue.Description);
    }

    [Theory]
    [InlineData("[JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]\npublic object Payload { get; set; }")]
    [InlineData("[JsonProperty(ItemTypeNameHandling = TypeNameHandling.All)]\npublic List<object> Items { get; set; }")]
    [InlineData("serializer.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto;")]
    [InlineData("JsonConvert.DefaultSettings = () => new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };")]
    public async Task Fires_ForTypeNameHandlingOnAttributesSerializersAndDefaults(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Fact]
    public async Task Warns_InsteadOfErroring_WhenASerializationBinderIsAssigned()
    {
        using var project = TestProject.Create("""
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new KnownTypesBinder(),
            };
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Contains("make sure the binder allow-lists the expected types", issue.Description);
    }

    [Theory]
    [InlineData("var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };")]
    [InlineData("if (settings.TypeNameHandling == TypeNameHandling.Auto) { }")]
    [InlineData("var json = JsonSerializer.Deserialize<Order>(body);")]
    [InlineData("[JsonDerivedType(typeof(Card), \"card\")]\npublic abstract record Payment;")]
    [InlineData("public sealed class MyBinaryFormatterWrapper { }")]
    [InlineData("// var formatter = new BinaryFormatter();")]
    [InlineData("var serializer = new JavaScriptSerializer();")]
    public async Task DoesNotFire_ForSafeSerialization(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ReportsTheErrorFirst_WhenBothSeveritiesArePresent()
    {
        using var project = TestProject.Create("""
            var a = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto, SerializationBinder = binder };
            var f = new BinaryFormatter();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Equal(2, issue.LineNumber);
    }
}
