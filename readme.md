# ASP.NET Thread Pool Demo

This project demonstrates how the .NET thread pool behavior affects API throughput and response times, especially under CPU-constrained environments. It shows the impact of thread pool priming on performance and provides comprehensive monitoring of thread pool metrics.

## Key Concepts Demonstrated

### Thread Pool Limitations
- **Thread Pool Exhaustion**: When all worker threads are busy, new requests must wait
- **Thread Creation Overhead**: .NET creates threads on-demand, which can cause initial latency
- **CPU-bound vs I/O-bound Operations**: Different impacts on thread pool utilization
- **Blocking Operations**: How synchronous blocking calls can starve the thread pool

### Thread Pool Priming Benefits
- **Reduced Cold Start Latency**: Pre-warmed threads are immediately available
- **Better Performance Under Load**: Consistent response times during traffic spikes
- **Resource Optimization**: Optimal thread utilization from startup

## Project Structure

```
├── Program.cs                 # Main application with thread pool monitoring
├── Controllers/
│   └── LoadTestController.cs  # API endpoints for testing different scenarios
├── Dockerfile                 # Container configuration
├── docker-compose.yml         # Multi-container setup with CPU limits
├── load-tests.js             # K6 load testing scripts
├── run-tests.sh              # Automated test execution
├── appsettings.json          # Application configuration
└── README.md                 # This file
```

## API Endpoints

### Thread Pool Monitoring
- `GET /api/LoadTest/stats` - Current thread pool statistics
- `POST /api/LoadTest/prime` - Manually prime the thread pool
- `GET /api/LoadTest/health` - Health check endpoint

### Load Testing Endpoints
- `GET /api/LoadTest/fast` - Fast I/O-bound operation (50ms delay)
- `GET /api/LoadTest/slow` - Slow I/O-bound operation (2s delay)
- `GET /api/LoadTest/cpu-bound` - CPU-intensive operation
- `GET /api/LoadTest/mixed` - Mixed I/O and CPU operation
- `GET /api/LoadTest/blocking` - Blocking synchronous operation (anti-pattern)

## Running the Demo

### Prerequisites
- Docker and Docker Compose
- K6 load testing tool
- curl (for health checks)
- Python 3 (for JSON formatting)

### Quick Start
```bash
# Make the scripts executable
chmod +x run-tests.sh
chmod +x run-isolated-tests.sh
chmod +x test-isolation.sh

# Run the complete demo with separate base/primed testing
./run-tests.sh

# Or run completely isolated comparison tests
./run-isolated-tests.sh

# Or run simple isolation verification
./test-isolation.sh
```

### Manual Setup
```bash
# Build and start containers
docker-compose up -d --build

# Wait for services to start
sleep 30

# Check health
curl http://localhost:5000/api/LoadTest/health
curl http://localhost:5001/api/LoadTest/health

# Run load tests
k6 run load-tests.js

# View logs
docker-compose logs threadpool-demo
docker-compose logs threadpool-demo-primed
```

## Container Configuration

The demo runs two identical APIs with different configurations:

### Base API (Port 8001)
- No thread pool priming
- Standard .NET thread pool behavior
- CPU limit: 250m (0.25 CPU cores)

### Primed API (Port 8002)
- Thread pool primed on startup
- Pre-warmed worker threads
- CPU limit: 250m (0.25 CPU cores)

## Test Isolation

The load tests are designed to ensure proper isolation between primed and non-primed execution:

### Concurrent Tests (`load-tests.js`)
- Separate scenarios for base and primed APIs
- Each test function targets only one API endpoint
- Eliminates random URL selection for cleaner comparison

### Sequential Tests (`isolated-load-tests.js`)
- Tests run in phases to avoid interference
- Base API tested first, then primed API
- Final side-by-side comparison phase

### Test Scripts

#### Main Test Script (`run-tests.sh`)
- Comprehensive testing with separate phases
- Phase 1: Tests base API only (3 different test types)
- Phase 2: Primes the thread pool (simulating pod restart)
- Phase 3: Tests primed API only (same 3 test types)
- Phase 4: Advanced concurrent comparison tests

#### Isolated Test Script (`run-isolated-tests.sh`)
- Completely isolated comparison testing
- Tests base API in isolation (60s mixed workload)
- Primes thread pool (simulating pod restart)
- Tests primed API in isolation (60s mixed workload)
- Direct comparison of results

#### Isolation Verification (`test-isolation.sh`)
- Simple script to verify isolation is working
- Tests each API separately, then together
- Provides clear before/after thread pool stats

## Thread Pool Metrics

The application logs detailed thread pool statistics:

```json
{
  "Context": "Periodic Monitor",
  "WorkerThreads": {
    "Active": 2,
    "Available": 30,
    "Max": 32,
    "Min": 4,
    "Utilization": "6.3%"
  },
  "CompletionPortThreads": {
    "Active": 1,
    "Available": 999,
    "Max": 1000,
    "Min": 4,
    "Utilization": "0.1%"
  },
  "ProcessorCount": 1,
  "TotalThreads": 15
}
```

## Load Testing Scenarios

### 1. Fast Operations Test
- **Purpose**: Test I/O-bound operations with minimal processing
- **Load**: 20 concurrent users
- **Duration**: 60 seconds
- **Expected**: Minimal thread pool impact, good performance

### 2. CPU-Bound Operations Test
- **Purpose**: Test worker thread exhaustion
- **Load**: 8 concurrent users
- **Duration**: 60 seconds
- **Expected**: High worker thread utilization, potential queueing

### 3. Blocking Operations Test
- **Purpose**: Demonstrate thread pool starvation
- **Load**: 10 concurrent users
- **Duration**: 60 seconds
- **Expected**: Severe performance degradation, thread pool exhaustion

### 4. Mixed Workload Test
- **Purpose**: Real-world scenario with varied operations
- **Load**: Multiple scenarios running simultaneously
- **Duration**: 2 minutes
- **Expected**: Comprehensive thread pool behavior demonstration

## Key Observations

### Without Thread Pool Priming
- Higher latency on first requests
- Gradual performance improvement as threads are created
- Potential thread starvation under heavy load
- Inconsistent response times

### With Thread Pool Priming
- Consistent low latency from startup
- Better performance under immediate load
- More predictable resource utilization
- Reduced cold start effects

### CPU Constraints Impact
- 250m CPU limit simulates resource-constrained environments
- Thread creation becomes more expensive
- CPU-bound operations compete for limited resources
- Thread pool exhaustion occurs more quickly

## Monitoring and Analysis

### Real-time Monitoring
```bash
# Watch thread pool stats
watch -n 2 'curl -s http://localhost:5000/api/LoadTest/stats | python3 -m json.tool'

# Monitor container resources
docker stats
```

### Log Analysis
```bash
# Filter thread pool logs
docker-compose logs | grep "ThreadPool Stats"

# Monitor specific operations
docker-compose logs | grep "operation started"
```

## Best Practices Demonstrated

### ✅ Good Practices
- **Async/Await**: Proper asynchronous programming patterns
- **Thread Pool Priming**: Pre-warming threads for better performance
- **Resource Monitoring**: Comprehensive thread pool metrics
- **Proper I/O Handling**: Non-blocking I/O operations

### ❌ Anti-patterns Shown
- **Blocking Operations**: Using `Thread.Sleep()` instead of `await Task.Delay()`
- **Thread Pool Abuse**: CPU-intensive work on thread pool threads
- **No Monitoring**: Running without observability

## Configuration Options

### Environment Variables
- `PrimeThreadPool`: Enable/disable thread pool priming
- `ThreadPoolMonitoringInterval`: Monitoring frequency (milliseconds)
- `ASPNETCORE_ENVIRONMENT`: Application environment

### Thread Pool Settings
- Monitor worker thread utilization
- Track completion port thread usage
- Observe thread creation patterns
- Measure response time impacts

## Troubleshooting

### Common Issues
1. **Containers not starting**: Check Docker resources and port availability
2. **High response times**: Monitor thread pool exhaustion in logs
3. **K6 test failures**: Verify service health before running tests
4. **Resource limits**: Ensure Docker has sufficient resources allocated

### Performance Tuning
1. **Adjust thread pool limits**: Modify min/max thread settings
2. **Optimize CPU allocation**: Balance between constraint and performance
3. **Monitor GC pressure**: Watch for garbage collection impact
4. **Profile async operations**: Identify blocking code patterns

## Cleanup

```bash
# Stop and remove containers
docker-compose down

# Remove images (optional)
docker-compose down --rmi all

# Remove volumes (optional)
docker-compose down --volumes
```

## Further Reading

- [.NET Thread Pool Documentation](https://docs.microsoft.com/en-us/dotnet/standard/threading/the-managed-thread-pool)
- [ASP.NET Core Performance Best Practices](https://docs.microsoft.com/en-us/aspnet/core/performance/performance-best-practices)
- [K6 Load Testing Guide](https://k6.io/docs/)
- [Docker Resource Constraints](https://docs.docker.com/config/containers/resource_constraints/)

## Contributing

Feel free to submit issues and enhancement requests. This demo is designed to be educational and can be extended with additional scenarios and monitoring capabilities.