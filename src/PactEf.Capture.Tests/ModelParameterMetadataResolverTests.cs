using Microsoft.EntityFrameworkCore;
using PactEf.Capture;
using PactEf.Core.Models;

namespace PactEf.Capture.Tests;

public class ModelParameterMetadataResolverTests
{
    private sealed class Order
    {
        public int Id { get; set; }
        public string Status { get; set; } = "";
    }

    private sealed class TestDbContext : DbContext
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseNpgsql("Host=localhost;Database=pactef-model-test");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(e =>
            {
                e.HasKey(o => o.Id);
                e.Property(o => o.Status).IsRequired().HasMaxLength(50);
            });
        }
    }

    [Fact]
    public void Enrich_InsertParameterMatchingHasMaxLengthColumn_SetsMaxLengthFromModel()
    {
        using var context = new TestDbContext();
        var parameters = new List<ParameterMetadata>
        {
            new() { Name = "@p0", ClrType = "String", Size = 5 }
        };
        const string sql = "INSERT INTO \"Orders\" (\"Status\") VALUES (@p0) RETURNING \"Id\";";

        var enriched = ModelParameterMetadataResolver.Enrich(parameters, sql, context.Model);

        Assert.Equal(50, enriched[0].MaxLength);
        Assert.Equal(5, enriched[0].Size);
    }

    [Fact]
    public void Enrich_WhereEqualityParameterMatchingColumn_SetsMaxLengthFromModel()
    {
        using var context = new TestDbContext();
        var parameters = new List<ParameterMetadata>
        {
            new() { Name = "@__status_0", ClrType = "String" }
        };
        const string sql = "SELECT o.\"Id\", o.\"Status\" FROM \"Orders\" AS o WHERE o.\"Status\" = @__status_0";

        var enriched = ModelParameterMetadataResolver.Enrich(parameters, sql, context.Model);

        Assert.Equal(50, enriched[0].MaxLength);
    }

    [Fact]
    public void Enrich_UnmappableParameter_FallsBackToProviderMetadataWithoutThrowing()
    {
        var parameters = new List<ParameterMetadata>
        {
            new() { Name = "@p0", ClrType = "String", Size = 5 }
        };
        const string sql = "INSERT INTO \"NotInModel\" (\"Foo\") VALUES (@p0);";

        var enriched = ModelParameterMetadataResolver.Enrich(parameters, sql, null);

        Assert.Same(parameters, enriched);
        Assert.Null(enriched[0].MaxLength);
    }

    [Fact]
    public void Enrich_NullModel_ReturnsOriginalParametersUnchanged()
    {
        var parameters = new List<ParameterMetadata> { new() { Name = "@p0" } };

        var enriched = ModelParameterMetadataResolver.Enrich(parameters, "SELECT 1", null);

        Assert.Same(parameters, enriched);
    }

    [Fact]
    public void Enrich_RuntimeValueShorterThanMaxLength_StillReportsFullMaxLengthFromModel()
    {
        using var context = new TestDbContext();
        var parameters = new List<ParameterMetadata>
        {
            new() { Name = "@p0", ClrType = "String", Size = 5 }
        };
        const string sql = "INSERT INTO \"Orders\" (\"Status\") VALUES (@p0) RETURNING \"Id\";";

        var enriched = ModelParameterMetadataResolver.Enrich(parameters, sql, context.Model);

        // Runtime value ("hello", Size=5) is shorter than the schema's HasMaxLength(50);
        // captured contract metadata must reflect the schema limit, not the observed value.
        Assert.Equal(50, enriched[0].MaxLength);
    }
}
