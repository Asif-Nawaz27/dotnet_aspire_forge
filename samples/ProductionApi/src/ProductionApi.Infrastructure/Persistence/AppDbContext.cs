using Microsoft.EntityFrameworkCore;

namespace ProductionApi.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}