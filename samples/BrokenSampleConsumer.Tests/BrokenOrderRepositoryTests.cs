using BrokenSampleConsumer.Repository;
using BrokenSampleConsumer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using PactEf.Capture;
using Xunit;
using Xunit.Extensions.AssemblyFixture;

namespace BrokenSampleConsumer.Tests;

public class BrokenOrderRepositoryTests(SampleDatabaseFixture db)
    : IClassFixture<SampleDatabaseFixture>, IAssemblyFixture<PactEfAssemblyFixture>
{
    [Theory]
    [InlineData("ProductName", "ProductDescription", 1)]
    [InlineData("NiceDescription", null, 42)]
    [InlineData("EmptyDescription", "", 228)]
    public async Task CreateProductItemAsync_WhenOrderExists_CreatesProduct(string productName, string? description,
        int quantity)
    {
        // Arrange
        await using var ctx = db.CreateBrokenDbContext();
        var order = await ctx.Orders.FirstAsync();

        var repo = new BrokenOrderRepository(ctx);

        // Act
        await repo.CreateProductItemAsync(order.Id, productName, description, quantity);

        // Assert
        var createdItem = await ctx.OrderItems.Where(i =>
            i.OrderId == order.Id && i.ProductName == productName && i.Description == description &&
            i.Quantity == quantity).SingleAsync();

        Assert.NotNull(createdItem);
    }

    [Fact]
    public async Task UpdateProductItemDescription_WhenProductExists_ResetsProductDescription()
    {
        // Arrange
        await using var ctx = db.CreateBrokenDbContext();
        var order = await ctx.Orders.Include(o => o.Items).FirstAsync();

        var repo = new BrokenOrderRepository(ctx);

        // Act
        await repo.UpdateProductItemDescription(order.Items.First().Id, null);

        // Assert
        var createdItem = await ctx.OrderItems.Where(i =>
            i.OrderId == order.Id && i.ProductName == "Widget" &&
            i.Quantity == 2).SingleAsync();

        Assert.NotNull(createdItem);
    }
}