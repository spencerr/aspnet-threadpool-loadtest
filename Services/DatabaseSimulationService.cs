using Microsoft.EntityFrameworkCore;
using ThreadPoolDemo.Data;
using ThreadPoolDemo.Models;

namespace ThreadPoolDemo.Services;

public interface IDatabaseSimulationService
{
    Task<User?> GetUserByIdAsync(int userId);
    Task<List<Order>> GetUserOrdersAsync(int userId, int limit = 10);
    Task<Order?> CreateOrderAsync(int userId, List<(int productId, int quantity)> items);
    Task<List<Product>> GetProductsAsync(string? category = null, int limit = 20);
    Task<Product?> GetProductByIdAsync(int productId);
    Task<UserSession> CreateUserSessionAsync(int userId, string ipAddress, string userAgent);
    Task<bool> UpdateUserLastLoginAsync(int userId);
    Task<List<Order>> GetRecentOrdersAsync(int limit = 50);
    Task<decimal> GetUserTotalSpentAsync(int userId);
    Task<int> GetProductStockAsync(int productId);
    Task<bool> UpdateProductStockAsync(int productId, int newStock);
}

public class DatabaseSimulationService : IDatabaseSimulationService
{
    private readonly LoadTestDbContext _context;
    private readonly ILogger<DatabaseSimulationService> _logger;

    public DatabaseSimulationService(LoadTestDbContext context, ILogger<DatabaseSimulationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        // Simulate network latency
        await Task.Delay(Random.Shared.Next(5, 15));
        
        return await _context.Users
            .Include(u => u.Sessions.Where(s => s.IsActive))
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<List<Order>> GetUserOrdersAsync(int userId, int limit = 10)
    {
        // Simulate network latency
        await Task.Delay(Random.Shared.Next(10, 25));
        
        return await _context.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.OrderDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Order?> CreateOrderAsync(int userId, List<(int productId, int quantity)> items)
    {
        // Simulate network latency for order creation
        await Task.Delay(Random.Shared.Next(20, 40));
        
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        var order = new Order
        {
            UserId = userId,
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending
        };

        decimal totalAmount = 0;
        foreach (var (productId, quantity) in items)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) continue;

            var itemTotal = product.Price * quantity;
            totalAmount += itemTotal;

            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                ProductName = product.Name,
                Quantity = quantity,
                UnitPrice = product.Price,
                TotalPrice = itemTotal
            });
        }

        order.TotalAmount = totalAmount;
        _context.Orders.Add(order);
        
        // Simulate additional database write latency
        await Task.Delay(Random.Shared.Next(15, 30));
        await _context.SaveChangesAsync();

        return order;
    }

    public async Task<List<Product>> GetProductsAsync(string? category = null, int limit = 20)
    {
        // Simulate network latency
        await Task.Delay(Random.Shared.Next(8, 20));
        
        var query = _context.Products.Where(p => p.IsActive);
        
        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(p => p.Category == category);
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(int productId)
    {
        // Simulate network latency
        await Task.Delay(Random.Shared.Next(5, 12));
        
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
    }

    public async Task<UserSession> CreateUserSessionAsync(int userId, string ipAddress, string userAgent)
    {
        // Simulate network latency
        await Task.Delay(Random.Shared.Next(10, 20));
        
        var session = new UserSession
        {
            UserId = userId,
            SessionToken = Guid.NewGuid().ToString("N"),
            StartTime = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsActive = true
        };

        _context.UserSessions.Add(session);
        await _context.SaveChangesAsync();

        return session;
    }

    public async Task<bool> UpdateUserLastLoginAsync(int userId)
    {
        // Simulate network latency
        await Task.Delay(Random.Shared.Next(8, 15));
        
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<Order>> GetRecentOrdersAsync(int limit = 50)
    {
        // Simulate network latency for complex query
        await Task.Delay(Random.Shared.Next(25, 50));
        
        return await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<decimal> GetUserTotalSpentAsync(int userId)
    {
        // Simulate network latency for aggregation query
        await Task.Delay(Random.Shared.Next(30, 60));
        
        return await _context.Orders
            .Where(o => o.UserId == userId && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => o.TotalAmount);
    }

    public async Task<int> GetProductStockAsync(int productId)
    {
        // Simulate network latency
        await Task.Delay(Random.Shared.Next(5, 10));
        
        var product = await _context.Products.FindAsync(productId);
        return product?.StockQuantity ?? 0;
    }

    public async Task<bool> UpdateProductStockAsync(int productId, int newStock)
    {
        // Simulate network latency
        await Task.Delay(Random.Shared.Next(10, 20));
        
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return false;

        product.StockQuantity = newStock;
        await _context.SaveChangesAsync();

        return true;
    }
}
