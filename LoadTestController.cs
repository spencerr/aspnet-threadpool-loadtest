using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ThreadPoolDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoadTestController : ControllerBase
{
    private readonly ThreadPoolMonitor _monitor;
    private readonly ILogger<LoadTestController> _logger;

    public LoadTestController(ThreadPoolMonitor monitor, ILogger<LoadTestController> logger)
    {
        _monitor = monitor;
        _logger = logger;
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
        
        // Simulate redis + two db calls
        await Task.Delay(10);
        await Task.Delay(25);
        await Task.Delay(25);
        
        _logger.LogInformation("Fast operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);
        
        return Ok(new { 
            RequestId = requestId,
            ThreadId = threadId,
            Duration = "50ms",
            Type = "Fast I/O Operation"
        });
    }

    [HttpGet("slow")]
    public async Task<IActionResult> SlowOperation()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;
        
        _logger.LogInformation("Slow operation started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);
        
        // Simulate redis + slow db call
        await Task.Delay(10);
        await Task.Delay(500);
        
        _logger.LogInformation("Slow operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);
        
        return Ok(new { 
            RequestId = requestId,
            ThreadId = threadId,
            Duration = "2000ms",
            Type = "Slow I/O Operation"
        });
    }

    [HttpGet("cpu-bound")]
    public async Task<IActionResult> CpuBoundOperation()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;
        
        _logger.LogInformation("CPU-bound operation started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);

        await Task.Delay(10);
        await Task.Delay(25);
        
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
        
        _logger.LogInformation("CPU-bound operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}, Result: {Result}", 
            requestId, threadId, result);
        
        return Ok(new { 
            RequestId = requestId,
            ThreadId = threadId,
            Result = result,
            Type = "CPU-bound Operation"
        });
    }

    [HttpGet("mixed")]
    public async Task<IActionResult> MixedOperation()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;
        
        _logger.LogInformation("Mixed operation started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);
        
        // First some I/O
        await Task.Delay(100);
        
        // Then some CPU work
        var cpuResult = await Task.Run(() =>
        {
            var sum = 0L;
            for (int i = 0; i < 10_000_000; i++)
            {
                sum += i;
            }
            return sum;
        });
        
        // Then more I/O
        await Task.Delay(100);
        
        _logger.LogInformation("Mixed operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);
        
        return Ok(new { 
            RequestId = requestId,
            ThreadId = threadId,
            CpuResult = cpuResult,
            Type = "Mixed I/O and CPU Operation"
        });
    }

    [HttpGet("blocking")]
    public IActionResult BlockingOperation()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var threadId = Thread.CurrentThread.ManagedThreadId;
        
        _logger.LogInformation("Blocking operation started - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);
        
        // BAD: Blocking synchronous operation that ties up thread pool threads
        Thread.Sleep(1000);
        
        _logger.LogInformation("Blocking operation completed - RequestId: {RequestId}, ThreadId: {ThreadId}", requestId, threadId);
        
        return Ok(new { 
            RequestId = requestId,
            ThreadId = threadId,
            Duration = "1000ms",
            Type = "Blocking Synchronous Operation (BAD!)"
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
}