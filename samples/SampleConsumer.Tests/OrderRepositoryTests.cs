using PactEf.Capture;
using SampleConsumer.Repositories;
using SampleConsumer.Tests.Fixtures;
using SampleDb.Entities;
using Xunit.Extensions.AssemblyFixture;

namespace SampleConsumer.Tests;

public sealed class OrderRepositoryTests(DatabaseFixture db) : IClassFixture<DatabaseFixture>, IAssemblyFixture<PactEfAssemblyFixture>
{
    [Fact]
    public async Task GetByIdAsync_WhenOrderExists_ReturnsOrder()
    {
        // Arrange
        await using var ctx = db.CreateDbContext();
        var order = new Order { Status = "Pending", CreatedAt = DateTimeOffset.UtcNow };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var repo = new OrderRepository(ctx);

        // Act
        var result = await repo.GetByIdAsync(order.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task GetByStatusAsync_ReturnsMatchingOrders()
    {
        // Arrange
        await using var ctx = db.CreateDbContext();
        ctx.Orders.AddRange(
            new Order { Status = "Shipped", CreatedAt = DateTimeOffset.UtcNow },
            new Order { Status = "Pending", CreatedAt = DateTimeOffset.UtcNow });
        await ctx.SaveChangesAsync();

        var repo = new OrderRepository(ctx);

        // Act
        var results = await repo.GetByStatusAsync("Shipped");

        // Assert
        Assert.All(results, o => Assert.Equal("Shipped", o.Status));
    }

    [Fact]
    public async Task GetWithItemsAsync_ReturnsOrdersWithItems()
    {
        // Arrange
        await using var ctx = db.CreateDbContext();
        var order = new Order { Status = "Processing", CreatedAt = DateTimeOffset.UtcNow };
        order.Items.Add(new OrderItem { ProductName = "Widget", Quantity = 3 });
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var repo = new OrderRepository(ctx);

        // Act
        var results = await repo.GetWithItemsAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, o => Assert.NotNull(o.Items));
    }
}

public class DiagnosticsTest(DatabaseFixture db) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public void EnvironmentIsSetCorrectly()
    {
        // Act
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        // Assert
        Assert.Equal("Testing", env);
    }

    [Fact]
    public void InterceptorIsRealType()
    {
        // Arrange
        var options = new CaptureOptions { ConsumerName = "Test" };

        // Act
        var interceptor = PactEfCaptureInterceptor.Create(o => o.ConsumerName = "Test");

        // Assert
        Assert.IsType<PactEfCaptureInterceptor>(interceptor);
    }
}
