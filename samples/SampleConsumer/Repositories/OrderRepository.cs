using Microsoft.EntityFrameworkCore;
using SampleDb;
using SampleDb.Entities;

namespace SampleConsumer.Repositories;

public sealed class OrderRepository(SampleDbContext db)
{
    public Task<Order?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<List<Order>> GetByStatusAsync(string status, CancellationToken ct = default) =>
        db.Orders.Where(o => o.Status == status).ToListAsync(ct);

    public Task<List<Order>> GetWithItemsAsync(CancellationToken ct = default) =>
        db.Orders.Include(o => o.Items).ToListAsync(ct);
}
