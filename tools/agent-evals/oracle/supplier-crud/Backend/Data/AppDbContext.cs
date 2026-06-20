using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

// Oracle overlay: extends the thin baseline context with the Supplier set.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
}
