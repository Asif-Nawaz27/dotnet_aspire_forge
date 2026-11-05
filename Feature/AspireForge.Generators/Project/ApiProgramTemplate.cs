namespace AspireForge.Generators.Project;

// The Api's Program.cs. Replaces the `dotnet new webapi` sample (a weather-forecast endpoint and
// nothing production-shaped) with a pipeline that passes AspireForge's own rules out of the box:
// global exception handling with problem details (REL002), HSTS outside development (SEC006),
// response compression (PERF001), and per-client rate limiting (PERF002) - in the middleware order
// SEC009 expects. Health checks and telemetry come from ServiceDefaults (`add telemetry` runs as part
// of `new`).
//
// The `WebApplication.CreateBuilder`, `builder.Build()`, and `app.Run(` lines are the anchors the
// `add` installers and `fix` insert next to (ProgramFileEditor) - keep them on lines of their own.
internal static class ApiProgramTemplate
{
    public static string ProgramCs(string projectName) => $$"""
        using System.Threading.RateLimiting;

        var builder = WebApplication.CreateBuilder(args);

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        // Unhandled exceptions become RFC 9457 problem details instead of a bare 500 page.
        builder.Services.AddProblemDetails();

        builder.Services.AddResponseCompression();

        // A per-client request budget for every endpoint. Behind a reverse proxy, configure forwarded
        // headers so this partitions by the real client address rather than the proxy's.
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));
        });

        var app = builder.Build();

        // First, so it also catches exceptions thrown by the middleware below.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }
        else
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseResponseCompression();
        app.UseRateLimiter();

        app.MapGet("/", () => Results.Ok(new { service = "{{projectName}}.Api", status = "running" }))
            .WithName("GetStatus");

        app.Run();

        // Exposed so integration tests can host the app with WebApplicationFactory<Program>.
        public partial class Program;

        """;
}
