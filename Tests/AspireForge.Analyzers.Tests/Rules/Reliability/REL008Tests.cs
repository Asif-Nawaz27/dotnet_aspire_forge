using AspireForge.Analyzers.Rules.Reliability;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Reliability;

public class REL008Tests
{
    private readonly REL008AsyncVoidMethod _rule = new();

    [Fact]
    public async Task Fires_ForAnAsyncVoidMethod_WithFileAndLine()
    {
        using var project = TestProject.Create("""
            public class OrderNotifier
            {
                public async void Notify(Order order)
                {
                    await _email.SendAsync(order.CustomerEmail);
                }
            }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("REL008", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains("Notify() at Program.cs:3", issue.Description);
    }

    [Theory]
    [InlineData("private async void Refresh() { await Task.Delay(1); }", "Refresh")]
    [InlineData("public static async void Fire<T>(T item) { await Task.Yield(); }", "Fire")]
    [InlineData("internal async void HandleAsync(object state) { await Task.Yield(); }", "HandleAsync")]
    [InlineData("protected override async void OnStarted() { await Task.Yield(); }", "OnStarted")]
    [InlineData("void Outer() { async void Inner() { await Task.Yield(); } Inner(); }", "Inner")]
    [InlineData("[HttpPost]\npublic async void Create(OrderDto dto) { await _service.CreateAsync(dto); }", "Create")]
    public async Task Fires_ForMethodsAndLocalFunctions(string source, string method)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains($"{method}()", issue!.Description);
    }

    // The delegate type forces void here - the one place async void is unavoidable.
    [Theory]
    [InlineData("private async void OnClick(object sender, EventArgs e) { await Task.Yield(); }")]
    [InlineData("private async void OnLoaded(object? sender, RoutedEventArgs e) { await Task.Yield(); }")]
    [InlineData("private async void OnElapsed(object source, System.Timers.ElapsedEventArgs args) { await Task.Yield(); }")]
    public async Task DoesNotFire_ForEventHandlers(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("public async Task NotifyAsync() { await Task.Yield(); }")]
    [InlineData("public async ValueTask DisposeAsync() { await Task.Yield(); }")]
    [InlineData("button.Click += async (s, e) => await SaveAsync();")]
    [InlineData("public void Run() { }")]
    [InlineData("// public async void Notify() { }")]
    [InlineData("var message = \"never use async void\";")]
    public async Task DoesNotFire_ForTaskReturningOrNonAsyncCode(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task StillFires_WhenAnEventHandlerIsAlsoPresent()
    {
        using var project = TestProject.Create("""
            private async void OnClick(object sender, EventArgs e) { await SaveAsync(); }
            private async void SaveLater(Order order) { await SaveAsync(); }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(2, issue!.LineNumber);
        Assert.DoesNotContain("OnClick", issue.Description);
    }

    [Fact]
    public async Task ListsAtMostThreeMethods_AndCountsTheRest()
    {
        using var project = TestProject.Create(string.Join(
            "\n",
            Enumerable.Range(1, 5).Select(i => $"async void Work{i}() {{ await Task.Yield(); }}")));

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains("(and 2 more)", issue!.Description);
    }
}
