namespace SampleDb.Entities;

public sealed class Order
{
    public int Id { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
