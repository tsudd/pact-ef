using Microsoft.EntityFrameworkCore;
using PactEf.Capture;
using SampleDb;
using SampleDb.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace BrokenSampleConsumer.Tests.Fixtures;

public sealed class SampleDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().Build();

    public SampleDbContextBroken CreateBrokenDbContext()
    {
        var options = new DbContextOptionsBuilder<SampleDbContextBroken>()
            .UseNpgsql(_container.GetConnectionString())
            .AddPactEfCapture(o => o.ConsumerName = "BrokenSampleConsumer")
            .Options;

        return new SampleDbContextBroken(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Apply migrations
        await using var ctx = CreateBrokenDbContext();
        await ctx.Database.MigrateAsync();
        
        // Seed with order
        ctx.Orders.Add(new Order
        {
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new()
                {
                    ProductName = "Widget",
                    Quantity = 2,
                    Description = "A standard widget"
                }
            ]
        });
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
