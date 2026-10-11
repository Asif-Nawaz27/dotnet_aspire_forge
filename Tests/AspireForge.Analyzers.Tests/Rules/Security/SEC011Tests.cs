using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC011Tests
{
    private readonly SEC011SqlInjectionInRawSql _rule = new();

    [Fact]
    public async Task Fires_ForInterpolationInFromSqlRaw_WithFileAndLine()
    {
        using var project = TestProject.Create("""
            public async Task<List<User>> Find(string name) =>
                await db.Users
                    .FromSqlRaw($"SELECT * FROM users WHERE name = '{name}'")
                    .ToListAsync();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC011", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains("FromSqlRaw at Program.cs:3", issue.Description);
    }

    [Theory]
    [InlineData("db.Database.ExecuteSqlRaw($\"DELETE FROM orders WHERE id = {id}\");")]
    [InlineData("await db.Database.ExecuteSqlRawAsync($\"UPDATE t SET x = {x}\");")]
    [InlineData("db.Database.SqlQueryRaw<int>($\"SELECT count(*) FROM t WHERE a = {a}\");")]
    [InlineData("db.Users.FromSqlRaw($@\"SELECT * FROM users WHERE name = '{name}'\");")]
    [InlineData("db.Users.FromSqlRaw(@$\"SELECT * FROM users WHERE name = '{name}'\");")]
    [InlineData("db.Users.FromSqlRaw($\"\"\"\n  SELECT * FROM users WHERE name = '{name}'\n  \"\"\");")]
    [InlineData("db.Users.FromSqlRaw(\"SELECT * FROM users WHERE id = \" + id);")]
    [InlineData("db.Users.FromSqlRaw(@\"SELECT * FROM users WHERE id = \" + id);")]
    [InlineData("db.Users.FromSqlRaw(\"SELECT * \" + \"FROM users WHERE id = \" + id);")]
    [InlineData("db.Users.FromSqlRaw(string.Format(\"SELECT * FROM users WHERE id = {0}\", id));")]
    public async Task Fires_ForUnsafeArgumentsToEfCoreRawApis(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    // These overloads take a FormattableString and turn every hole into a DbParameter - the safe fix.
    [Theory]
    [InlineData("db.Users.FromSql($\"SELECT * FROM users WHERE name = {name}\");")]
    [InlineData("db.Database.ExecuteSql($\"DELETE FROM orders WHERE id = {id}\");")]
    [InlineData("await db.Database.ExecuteSqlAsync($\"DELETE FROM orders WHERE id = {id}\");")]
    [InlineData("db.Database.SqlQuery<int>($\"SELECT count(*) FROM t WHERE a = {a}\");")]
    [InlineData("db.Database.ExecuteSqlCommand($\"DELETE FROM orders WHERE id = {id}\");")]
    public async Task DoesNotFire_ForEfCoreApisThatParameteriseInterpolation(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("db.Users.FromSql(\"SELECT * FROM users WHERE id = \" + id);")]
    [InlineData("db.Database.ExecuteSql(string.Format(\"DELETE FROM t WHERE id = {0}\", id));")]
    public async Task Fires_ForConcatenationEvenInSafeEfCoreApis(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("db.Users.FromSqlRaw(\"SELECT * FROM users WHERE id = {0}\", id);")]
    [InlineData("db.Users.FromSqlRaw(\"SELECT * FROM users WHERE id = @id\", new SqlParameter(\"id\", id));")]
    [InlineData("db.Users.FromSqlRaw(\"SELECT * FROM users \" + \"ORDER BY name\");")]
    [InlineData("db.Users.FromSqlRaw($\"SELECT * FROM users\");")]
    [InlineData("db.Users.FromSqlRaw($\"SELECT '{{literal braces}}' FROM users\");")]
    [InlineData("db.Users.FromSqlRaw(sql);")]
    [InlineData("// db.Users.FromSqlRaw($\"SELECT * FROM users WHERE name = '{name}'\");")]
    public async Task DoesNotFire_ForParameterisedConstantOrUnknownSql(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("using var cmd = new SqlCommand($\"SELECT * FROM t WHERE id = {id}\", connection);")]
    [InlineData("using var cmd = new NpgsqlCommand(\"SELECT * FROM t WHERE id = \" + id, connection);")]
    [InlineData("command.CommandText = $\"DELETE FROM t WHERE id = {id}\";")]
    [InlineData("command.CommandText = \"DELETE FROM t WHERE id = \" + id;")]
    public async Task Fires_ForAdoNetCommands(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("using var cmd = new SqlCommand(\"SELECT * FROM t WHERE id = @id\", connection);")]
    [InlineData("command.CommandText = \"DELETE FROM t WHERE id = @id\";")]
    [InlineData("if (command.CommandText == $\"x{y}\") { }")]
    public async Task DoesNotFire_ForParameterisedAdoNetCommands(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("var users = await connection.QueryAsync<User>($\"SELECT * FROM users WHERE name = '{name}'\");")]
    [InlineData("connection.Execute(\"DELETE FROM users WHERE id = \" + id);")]
    [InlineData("var u = connection.QueryFirstOrDefault<User>($\"SELECT * FROM users WHERE id = {id}\");")]
    public async Task Fires_ForDapper_WhenTheFileImportsDapper(string call)
    {
        using var project = TestProject.Create("using Dapper;\n" + call);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("using Dapper;\nvar users = await connection.QueryAsync<User>(\"SELECT * FROM users WHERE name = @name\", new { name });")]
    [InlineData("var result = handler.Execute($\"Processing {id}\");")]
    [InlineData("var items = repository.Query($\"name:{term}\");")]
    public async Task DoesNotFire_ForParameterisedDapperOrNonDapperLookAlikes(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ListsAtMostThreeLocations_AndCountsTheRest()
    {
        using var project = TestProject.Create(string.Join(
            "\n",
            Enumerable.Range(1, 5).Select(i => $"db.Database.ExecuteSqlRaw($\"DELETE FROM t{i} WHERE id = {{id}}\");")));

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains("(and 2 more)", issue!.Description);
        Assert.Contains("ExecuteSqlRaw at Program.cs:1", issue.Description);
    }
}
