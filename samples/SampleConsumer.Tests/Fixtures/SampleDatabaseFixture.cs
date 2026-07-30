using Microsoft.EntityFrameworkCore;
using PactEf.Capture;
using SampleDb;
using Testcontainers.PostgreSql;

namespace SampleConsumer.Tests.Fixtures;

public sealed class SampleDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().Build();

    public SampleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .AddPactEfCapture(o => o.ConsumerName = "SampleConsumer")
            .Options;

        return new SampleDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Apply migrations
        await using var ctx = CreateDbContext();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
