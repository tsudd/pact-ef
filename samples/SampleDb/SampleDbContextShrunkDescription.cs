using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SampleDb.Entities;

namespace SampleDb;

// Represents the database schema *after* a breaking migration shrinks
// OrderItems.Description from the consumer's captured contract (varchar(100))
// down to varchar(50). Used only to prove boundary-value replay catches this
// where a plain EXPLAIN of the captured INSERT would not.
public sealed class SampleDbContextShrunkDescription(DbContextOptions<SampleDbContextShrunkDescription> options)
    : DbContext(options)
{
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.Ignore(i => i.Order);
            e.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
            e.Property(i => i.Description).HasMaxLength(50);
        });
    }
}

public sealed class SampleDbContextShrunkDescriptionFactory : IDesignTimeDbContextFactory<SampleDbContextShrunkDescription>
{
    public SampleDbContextShrunkDescription CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SampleDbContextShrunkDescription>()
            .UseNpgsql("connectionString")
            .Options;

        return new SampleDbContextShrunkDescription(options);
    }
}
