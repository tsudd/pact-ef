namespace SampleDb.Entities;

public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public required string ProductName { get; set; }
    public int Quantity { get; set; }
}
