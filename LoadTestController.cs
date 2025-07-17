using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ThreadPoolDemo.Services;

namespace ThreadPoolDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoadTestController : ControllerBase
{
    private readonly ThreadPoolMonitor _monitor;
    private readonly ILogger<LoadTestController> _logger;
    private readonly IDatabaseSimulationService _databaseService;
    private readonly IRedisSimulationService _redisService;

    public LoadTestController(
        ThreadPoolMonitor monitor,
        ILogger<LoadTestController> logger,
        IDatabaseSimulationService databaseService,
        IRedisSimulationService redisService)
    {
        _monitor = monitor;
        _logger = logger;
        _databaseService = databaseService;
        _redisService = redisService;
    }

    [HttpGet("stats")]
    public IActionResult GetThreadPoolStats()
    {
        var stats = _monitor.GetCurrentStats();
        return Ok(stats);
    }

    [HttpGet("fast")]
    public async Task<IActionResult> FastOperation()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;

        _logger.LogInformation("Fast operation started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);

        var stopwatch = Stopwatch.StartNew();

        // Simulate Redis cache check for user session
        var userId = Random.Shared.Next(1, 51);
        var cacheKey = $"user_session:{userId}";
        var cachedSession = await _redisService.GetAsync<object>(cacheKey);

        // Simulate database calls
        var user = await _databaseService.GetUserByIdAsync(userId);
        var recentOrders = await _databaseService.GetUserOrdersAsync(userId, 5);

        // Cache the result
        if (user != null)
        {
            await _redisService.SetAsync(cacheKey, new { UserId = user.Id, Name = user.Name }, TimeSpan.FromMinutes(15));
        }

        stopwatch.Stop();

        _logger.LogInformation("Fast operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}, Duration: {Duration}ms",
            requestId, threadId, stopwatch.ElapsedMilliseconds);

        return Ok(new {
            RequestId = requestId,
            ThreadId = threadId,
            Duration = $"{stopwatch.ElapsedMilliseconds}ms",
            Type = "Fast I/O Operation",
            Data = new
            {
                User = user?.Name ?? "Unknown",
                OrderCount = recentOrders.Count,
                CacheHit = cachedSession != null
            }
        });
    }

    [HttpGet("slow")]
    public async Task<IActionResult> SlowOperation()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;

        _logger.LogInformation("Slow operation started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);

        var stopwatch = Stopwatch.StartNew();

        // Simulate Redis cache check
        var userId = Random.Shared.Next(1, 51);
        var cacheKey = $"user_analytics:{userId}";
        var cachedAnalytics = await _redisService.GetAsync<object>(cacheKey);

        if (cachedAnalytics == null)
        {
            // Simulate slow database analytics query
            var totalSpent = await _databaseService.GetUserTotalSpentAsync(userId);
            var recentOrders = await _databaseService.GetRecentOrdersAsync(20);

            // Additional slow operation - simulate complex aggregation
            await Task.Delay(Random.Shared.Next(300, 600));

            var analytics = new
            {
                UserId = userId,
                TotalSpent = totalSpent,
                RecentOrderCount = recentOrders.Count,
                GeneratedAt = DateTime.UtcNow
            };

            // Cache the expensive result
            await _redisService.SetAsync(cacheKey, analytics, TimeSpan.FromMinutes(30));

            stopwatch.Stop();

            _logger.LogInformation("Slow operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}, Duration: {Duration}ms",
                requestId, threadId, stopwatch.ElapsedMilliseconds);

            return Ok(new {
                RequestId = requestId,
                ThreadId = threadId,
                Duration = $"{stopwatch.ElapsedMilliseconds}ms",
                Type = "Slow I/O Operation",
                Data = analytics,
                CacheHit = false
            });
        }
        else
        {
            stopwatch.Stop();

            _logger.LogInformation("Slow operation completed (cached) - RequestId: {RequestId}, ThreadId: {ThreadId}, Duration: {Duration}ms",
                requestId, threadId, stopwatch.ElapsedMilliseconds);

            return Ok(new {
                RequestId = requestId,
                ThreadId = threadId,
                Duration = $"{stopwatch.ElapsedMilliseconds}ms",
                Type = "Slow I/O Operation (Cached)",
                Data = cachedAnalytics,
                CacheHit = true
            });
        }
    }

    [HttpGet("cpu-bound")]
    public async Task<IActionResult> CpuBoundOperation()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;

        _logger.LogInformation("CPU-bound operation started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);

        var stopwatch = Stopwatch.StartNew();

        // Simulate Redis cache check for computation result
        var computationKey = $"computation:{requestId[..4]}";
        var cachedResult = await _redisService.GetAsync<object>(computationKey);

        // Simulate initial database lookup
        var productId = Random.Shared.Next(1, 31);
        var product = await _databaseService.GetProductByIdAsync(productId);

        // Simulate CPU-bound work that consumes thread pool threads
        var result = await Task.Run(() =>
        {
            var sum = 0L;
            for (int i = 0; i < 50_000_000; i++)
            {
                sum += i;
            }
            return sum;
        });

        // Update product stock based on computation (simulate business logic)
        if (product != null)
        {
            var newStock = (int)(result % 100);
            await _databaseService.UpdateProductStockAsync(product.Id, newStock);
        }

        stopwatch.Stop();

        _logger.LogInformation("CPU-bound operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}, Result: {Result}, Duration: {Duration}ms",
            requestId, threadId, result, stopwatch.ElapsedMilliseconds);

        return Ok(new {
            RequestId = requestId,
            ThreadId = threadId,
            Duration = $"{stopwatch.ElapsedMilliseconds}ms",
            Result = result,
            Type = "CPU-bound Operation",
            Data = new
            {
                ProductProcessed = product?.Name ?? "Unknown",
                NewStock = product != null ? (int)(result % 100) : 0
            }
        });
    }

    [HttpGet("mixed")]
    public async Task<IActionResult> MixedOperation()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;

        _logger.LogInformation("Mixed operation started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);

        var stopwatch = Stopwatch.StartNew();

        // Phase 1: I/O - Get user and check cache
        var userId = Random.Shared.Next(1, 51);
        var user = await _databaseService.GetUserByIdAsync(userId);
        var sessionKey = $"active_session:{userId}";
        var activeSession = await _redisService.ExistsAsync(sessionKey);

        // Phase 2: CPU work - Process order data
        var cpuResult = await Task.Run(() =>
        {
            var sum = 0L;
            for (int i = 0; i < 10_000_000; i++)
            {
                sum += i;
            }
            return sum;
        });

        // Phase 3: More I/O - Create order based on CPU result
        if (user != null)
        {
            var products = await _databaseService.GetProductsAsync(limit: 3);
            if (products.Count > 0)
            {
                var orderItems = products.Take(2).Select(p => (p.Id, quantity: (int)(cpuResult % 3) + 1)).ToList();
                var order = await _databaseService.CreateOrderAsync(userId, orderItems);

                // Update session activity
                if (!activeSession)
                {
                    await _redisService.SetAsync(sessionKey, new { UserId = userId, LastActivity = DateTime.UtcNow }, TimeSpan.FromHours(1));
                }

                stopwatch.Stop();

                _logger.LogInformation("Mixed operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}, Duration: {Duration}ms",
                    requestId, threadId, stopwatch.ElapsedMilliseconds);

                return Ok(new {
                    RequestId = requestId,
                    ThreadId = threadId,
                    Duration = $"{stopwatch.ElapsedMilliseconds}ms",
                    CpuResult = cpuResult,
                    Type = "Mixed I/O and CPU Operation",
                    Data = new
                    {
                        User = user.Name,
                        OrderCreated = order?.OrderNumber ?? "Failed",
                        ProductsProcessed = products.Count,
                        SessionWasActive = activeSession
                    }
                });
            }
        }

        stopwatch.Stop();

        _logger.LogInformation("Mixed operation completed (no order) - RequestId: {RequestId}, ThreadId: {ThreadId}, Duration: {Duration}ms",
            requestId, threadId, stopwatch.ElapsedMilliseconds);

        return Ok(new {
            RequestId = requestId,
            ThreadId = threadId,
            Duration = $"{stopwatch.ElapsedMilliseconds}ms",
            CpuResult = cpuResult,
            Type = "Mixed I/O and CPU Operation",
            Data = new
            {
                User = user?.Name ?? "Unknown",
                OrderCreated = "None",
                ProductsProcessed = 0,
                SessionWasActive = activeSession
            }
        });
    }

    [HttpGet("blocking")]
    public IActionResult BlockingOperation()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;

        _logger.LogInformation("Blocking operation started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);

        var stopwatch = Stopwatch.StartNew();

        // BAD: Blocking synchronous operation that ties up thread pool threads
        // This simulates a poorly written synchronous database call
        Thread.Sleep(Random.Shared.Next(800, 1200));

        // Simulate additional blocking work
        var userId = Random.Shared.Next(1, 51);
        var blockingResult = 0;

        // More blocking work - simulating synchronous processing
        for (int i = 0; i < 1_000_000; i++)
        {
            blockingResult += i % 100;
        }

        stopwatch.Stop();

        _logger.LogInformation("Blocking operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}, Duration: {Duration}ms",
            requestId, threadId, stopwatch.ElapsedMilliseconds);

        return Ok(new {
            RequestId = requestId,
            ThreadId = threadId,
            Duration = $"{stopwatch.ElapsedMilliseconds}ms",
            Type = "Blocking Synchronous Operation (BAD!)",
            Data = new
            {
                ProcessedUserId = userId,
                BlockingResult = blockingResult,
                Warning = "This operation blocks thread pool threads!"
            }
        });
    }

    [HttpPost("prime")]
    public async Task<IActionResult> PrimeThreadPool()
    {
        await _monitor.PrimeThreadPoolAsync();
        return Ok(new { Message = "Thread pool primed successfully" });
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount
        });
    }

    [HttpGet("database-info")]
    public async Task<IActionResult> GetDatabaseInfo()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;

        _logger.LogInformation("Database info request started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);

        var stopwatch = Stopwatch.StartNew();

        // Get database statistics
        var userCount = await _databaseService.GetUserOrdersAsync(1, 1000);
        var products = await _databaseService.GetProductsAsync(limit: 5);
        var recentOrders = await _databaseService.GetRecentOrdersAsync(10);

        // Check Redis cache status
        var cacheKeys = await _redisService.GetKeysAsync("*");

        stopwatch.Stop();

        _logger.LogInformation("Database info request completed - RequestId: {RequestId}, ThreadId: {ThreadId}, Duration: {Duration}ms",
            requestId, threadId, stopwatch.ElapsedMilliseconds);

        return Ok(new
        {
            RequestId = requestId,
            ThreadId = threadId,
            Duration = $"{stopwatch.ElapsedMilliseconds}ms",
            DatabaseStats = new
            {
                SampleUserOrderCount = userCount.Count,
                ProductCount = products.Count,
                RecentOrderCount = recentOrders.Count,
                SampleProducts = products.Select(p => new { p.Name, p.Price, p.Category }).ToList()
            },
            CacheStats = new
            {
                ActiveCacheKeys = cacheKeys.Count,
                SampleKeys = cacheKeys.Take(5).ToList()
            }
        });
    }
}