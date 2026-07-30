using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SampleDb.Entities;

namespace SampleDb;

public sealed class SampleDbContextBroken(DbContextOptions<SampleDbContextBroken> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Status).IsRequired().HasMaxLength(50);
            e.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
            e.Property(i => i.Description).HasMaxLength(1000);
        });
    }
}

public sealed class SampleDbContextBrokenFactory : IDesignTimeDbContextFactory<SampleDbContextBroken>
{
    public SampleDbContextBroken CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SampleDbContextBroken>()
            .UseNpgsql("connectionString")
            .Options;

        return new SampleDbContextBroken(options);
    }
}
