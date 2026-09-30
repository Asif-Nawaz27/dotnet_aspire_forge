using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace App.Infrastructure.Persistence;

// Lets `dotnet ef` build the context from this project alone, so the Api host doesn't need an EF
// Core Design reference. Only used by tooling - the connection string is never opened for
// `migrations add`, and `database update` reads it from the environment.
//   dotnet ef migrations add <Name> --project Feature/App.Infrastructure --output-dir Persistence/Migrations
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? string.Empty;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
