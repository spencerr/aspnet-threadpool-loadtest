using Microsoft.EntityFrameworkCore;
using ThreadPoolDemo.Data;
using ThreadPoolDemo.Models;

namespace ThreadPoolDemo.Services;

public class DataSeedingService
{
    private readonly LoadTestDbContext _context;
    private readonly ILogger<DataSeedingService> _logger;

    public DataSeedingService(LoadTestDbContext context, ILogger<DataSeedingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedDataAsync()
    {
        try
        {
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Check if data already exists
            if (await _context.Users.AnyAsync())
            {
                _logger.LogInformation("Database already contains data, skipping seeding");
                return;
            }

            _logger.LogInformation("Starting database seeding...");

            // Seed Products first
            var products = await SeedProductsAsync();
            
            // Seed Users
            var users = await SeedUsersAsync();
            
            // Seed Orders and OrderItems
            await SeedOrdersAsync(users, products);
            
            // Seed User Sessions
            await SeedUserSessionsAsync(users);

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during database seeding");
            throw;
        }
    }

    private async Task<List<Product>> SeedProductsAsync()
    {
        var categories = new[] { "Electronics", "Books", "Clothing", "Home", "Sports", "Toys" };
        var products = new List<Product>();

        var productNames = new[]
        {
            "Laptop Computer", "Wireless Mouse", "Keyboard", "Monitor", "Headphones",
            "Programming Book", "Fiction Novel", "Technical Manual", "Biography", "Cookbook",
            "T-Shirt", "Jeans", "Sneakers", "Jacket", "Hat",
            "Coffee Maker", "Blender", "Vacuum Cleaner", "Lamp", "Pillow",
            "Basketball", "Tennis Racket", "Running Shoes", "Yoga Mat", "Dumbbells",
            "Board Game", "Puzzle", "Action Figure", "Doll", "Building Blocks"
        };

        for (int i = 0; i < productNames.Length; i++)
        {
            products.Add(new Product
            {
                Name = productNames[i],
                Description = $"High quality {productNames[i].ToLower()} for everyday use",
                Price = Random.Shared.Next(10, 1000),
                StockQuantity = Random.Shared.Next(0, 100),
                Category = categories[i % categories.Length],
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 365)),
                IsActive = true
            });
        }

        _context.Products.AddRange(products);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Seeded {Count} products", products.Count);
        return products;
    }

    private async Task<List<User>> SeedUsersAsync()
    {
        var firstNames = new[] { "John", "Jane", "Mike", "Sarah", "David", "Lisa", "Chris", "Emma", "Alex", "Maria" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };
        var users = new List<User>();

        for (int i = 0; i < 50; i++)
        {
            var firstName = firstNames[Random.Shared.Next(firstNames.Length)];
            var lastName = lastNames[Random.Shared.Next(lastNames.Length)];
            
            users.Add(new User
            {
                Name = $"{firstName} {lastName}",
                Email = $"{firstName.ToLower()}.{lastName.ToLower()}{i}@example.com",
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 730)),
                LastLoginAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 30)),
                IsActive = Random.Shared.Next(100) > 10 // 90% active users
            });
        }

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Seeded {Count} users", users.Count);
        return users;
    }

    private async Task SeedOrdersAsync(List<User> users, List<Product> products)
    {
        var orders = new List<Order>();
        var orderItems = new List<OrderItem>();
        var statuses = Enum.GetValues<OrderStatus>();

        for (int i = 0; i < 200; i++)
        {
            var user = users[Random.Shared.Next(users.Count)];
            var orderDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 180));
            
            var order = new Order
            {
                UserId = user.Id,
                OrderNumber = $"ORD-{orderDate:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                OrderDate = orderDate,
                Status = statuses[Random.Shared.Next(statuses.Length)],
                Notes = Random.Shared.Next(100) > 70 ? "Special delivery instructions" : null
            };

            // Add 1-5 items per order
            var itemCount = Random.Shared.Next(1, 6);
            decimal totalAmount = 0;

            for (int j = 0; j < itemCount; j++)
            {
                var product = products[Random.Shared.Next(products.Count)];
                var quantity = Random.Shared.Next(1, 4);
                var totalPrice = product.Price * quantity;
                totalAmount += totalPrice;

                orderItems.Add(new OrderItem
                {
                    Order = order,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = quantity,
                    UnitPrice = product.Price,
                    TotalPrice = totalPrice
                });
            }

            order.TotalAmount = totalAmount;
            orders.Add(order);
        }

        _context.Orders.AddRange(orders);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Seeded {OrderCount} orders with {ItemCount} order items", orders.Count, orderItems.Count);
    }

    private async Task SeedUserSessionsAsync(List<User> users)
    {
        var userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 14_7_1 like Mac OS X) AppleWebKit/605.1.15",
            "Mozilla/5.0 (Android 11; Mobile; rv:68.0) Gecko/68.0 Firefox/88.0"
        };

        var sessions = new List<UserSession>();

        foreach (var user in users.Take(30)) // Create sessions for 30 users
        {
            var sessionCount = Random.Shared.Next(1, 5);
            
            for (int i = 0; i < sessionCount; i++)
            {
                var startTime = DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 30));
                var isActive = Random.Shared.Next(100) > 80; // 20% active sessions
                
                sessions.Add(new UserSession
                {
                    UserId = user.Id,
                    SessionToken = Guid.NewGuid().ToString("N"),
                    StartTime = startTime,
                    EndTime = isActive ? null : startTime.AddMinutes(Random.Shared.Next(5, 240)),
                    IpAddress = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}",
                    UserAgent = userAgents[Random.Shared.Next(userAgents.Length)],
                    IsActive = isActive
                });
            }
        }

        _context.UserSessions.AddRange(sessions);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Seeded {Count} user sessions", sessions.Count);
    }
}
