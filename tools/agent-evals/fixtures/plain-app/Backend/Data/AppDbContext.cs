using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

// Thin EF Core context for the no-Spiderly baseline. Intentionally empty: feature entities, their
// DbSet<T>s, and migrations are added by the agent per task. Nothing here is generated.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
