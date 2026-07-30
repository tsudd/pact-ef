using SampleDb;
using SampleDb.Entities;

namespace BrokenSampleConsumer.Repository;

public sealed class BrokenOrderRepository(SampleDbContextBroken dbContext)
{
    public async Task CreateProductItemAsync(int orderId, string productName, string? productDescription, int quantity)
    {
        var item = new OrderItem
        {
            ProductName = productName,
            Description = productDescription, // broken contract
            Quantity = quantity,
            OrderId = orderId
        };
        await dbContext.OrderItems.AddAsync(item);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateProductItemDescription(int productId, string? description)
    {
        var item = await dbContext.OrderItems.FindAsync(productId);
        if (item != null)
        {
            item.Description = description;
            await dbContext.SaveChangesAsync();
        }
    }
}