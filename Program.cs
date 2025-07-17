using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThreadPoolDemo.Data;
using ThreadPoolDemo.Services;

var builder = WebApplication.CreateBuilder(args);

//Simulate pod on 4core vm
ThreadPool.SetMinThreads(4, 4);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add database context
builder.Services.AddDbContext<LoadTestDbContext>(options =>
    options.UseInMemoryDatabase("LoadTestDb"));

// Add simulation services
builder.Services.AddScoped<IDatabaseSimulationService, DatabaseSimulationService>();
builder.Services.AddSingleton<IRedisSimulationService, RedisSimulationService>();
builder.Services.AddScoped<DataSeedingService>();

// Add thread pool monitoring service
builder.Services.AddSingleton<ThreadPoolMonitor>();
builder.Services.AddHostedService<ThreadPoolMonitorService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var seedingService = scope.ServiceProvider.GetRequiredService<DataSeedingService>();
    await seedingService.SeedDataAsync();
}

// Optional: Prime the thread pool on startup
var primeOnStartup = builder.Configuration.GetValue<bool>("PrimeThreadPool", false);
if (primeOnStartup)
{
    var monitor = app.Services.GetRequiredService<ThreadPoolMonitor>();
    await monitor.PrimeThreadPoolAsync();
}

app.Run();

// Thread Pool Monitor Service
public class ThreadPoolMonitor
{
    private readonly ILogger<ThreadPoolMonitor> _logger;
    private readonly object _lock = new object();
    private ThreadPoolStats _lastStats = new ThreadPoolStats();

    public ThreadPoolMonitor(ILogger<ThreadPoolMonitor> logger)
    {
        _logger = logger;
    }

    public ThreadPoolStats GetCurrentStats()
    {
        lock (_lock)
        {
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
            ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);

            var stats = new ThreadPoolStats
            {
                Timestamp = DateTime.UtcNow,
                AvailableWorkerThreads = availableWorkerThreads,
                AvailableCompletionPortThreads = availableCompletionPortThreads,
                MaxWorkerThreads = maxWorkerThreads,
                MaxCompletionPortThreads = maxCompletionPortThreads,
                MinWorkerThreads = minWorkerThreads,
                MinCompletionPortThreads = minCompletionPortThreads,
                ActiveWorkerThreads = maxWorkerThreads - availableWorkerThreads,
                ActiveCompletionPortThreads = maxCompletionPortThreads - availableCompletionPortThreads,
                ProcessorCount = Environment.ProcessorCount,
                ThreadCount = Process.GetCurrentProcess().Threads.Count
            };

            _lastStats = stats;
            return stats;
        }
    }

    public void LogStats(string context = "")
    {
        var stats = GetCurrentStats();
        
        var logData = new
        {
            Context = context,
            Timestamp = stats.Timestamp,
            WorkerThreads = new
            {
                Active = stats.ActiveWorkerThreads,
                Available = stats.AvailableWorkerThreads,
                Max = stats.MaxWorkerThreads,
                Min = stats.MinWorkerThreads,
                Utilization = $"{(double)stats.ActiveWorkerThreads / stats.MaxWorkerThreads * 100:F1}%"
            },
            CompletionPortThreads = new
            {
                Active = stats.ActiveCompletionPortThreads,
                Available = stats.AvailableCompletionPortThreads,
                Max = stats.MaxCompletionPortThreads,
                Min = stats.MinCompletionPortThreads,
                Utilization = $"{(double)stats.ActiveCompletionPortThreads / stats.MaxCompletionPortThreads * 100:F1}%"
            },
            ProcessorCount = stats.ProcessorCount,
            TotalThreads = stats.ThreadCount
        };

        _logger.LogInformation("ThreadPool Stats: {Stats}", JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task PrimeThreadPoolAsync()
    {
        _logger.LogInformation("Starting thread pool priming...");
        
        // Get current thread pool settings
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
        ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);
        
        _logger.LogInformation("Thread pool settings - Max Worker: {MaxWorker}, Min Worker: {MinWorker}, Max IOCP: {MaxIOCP}, Min IOCP: {MinIOCP}",
            maxWorkerThreads, minWorkerThreads, maxCompletionPortThreads, minCompletionPortThreads);

        // Prime with CPU-bound tasks to warm up worker threads
        var primingTasks = new List<Task>();
        var threadsToWarm = Math.Max(Environment.ProcessorCount * 100, minWorkerThreads);

        ThreadPool.SetMinThreads(threadsToWarm, threadsToWarm);

        _logger.LogInformation("Warming up {ThreadCount} threads", threadsToWarm);

        for (int i = 0; i < threadsToWarm * 10; i++)
        {
            primingTasks.Add(Task.Run(() =>
            {
                var threadId = Thread.CurrentThread.ManagedThreadId;
                _logger.LogDebug("Priming thread {ThreadId}", threadId);

                Thread.SpinWait(50000);
            }));
        }

        await Task.WhenAll(primingTasks);
        
        // Small delay to let thread pool settle
        await Task.Delay(500);
        
        LogStats("After Priming");
        _logger.LogInformation("Thread pool priming completed");
    }
}

// Background service for periodic monitoring
public class ThreadPoolMonitorService : BackgroundService
{
    private readonly ThreadPoolMonitor _monitor;
    private readonly ILogger<ThreadPoolMonitorService> _logger;
    private readonly IConfiguration _configuration;

    public ThreadPoolMonitorService(ThreadPoolMonitor monitor, ILogger<ThreadPoolMonitorService> logger, IConfiguration configuration)
    {
        _monitor = monitor;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var monitoringInterval = _configuration.GetValue<int>("ThreadPoolMonitoringInterval", 5000);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _monitor.LogStats("Periodic Monitor");
                await Task.Delay(monitoringInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in thread pool monitoring");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}

// Data model for thread pool statistics
public class ThreadPoolStats
{
    public DateTime Timestamp { get; set; }
    public int AvailableWorkerThreads { get; set; }
    public int AvailableCompletionPortThreads { get; set; }
    public int MaxWorkerThreads { get; set; }
    public int MaxCompletionPortThreads { get; set; }
    public int MinWorkerThreads { get; set; }
    public int MinCompletionPortThreads { get; set; }
    public int ActiveWorkerThreads { get; set; }
    public int ActiveCompletionPortThreads { get; set; }
    public int ProcessorCount { get; set; }
    public int ThreadCount { get; set; }
}