#!/bin/bash

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== ASP.NET Thread Pool Demo Setup ===${NC}"

# Function to check if a command exists
check_command() {
    if ! command -v $1 &> /dev/null; then
        echo -e "${RED}Error: $1 is not installed${NC}"
        exit 1
    fi
}

# Check required tools
echo "Checking required tools..."
check_command docker
check_command docker-compose
check_command k6

# Build and start containers
echo -e "${YELLOW}Building and starting containers...${NC}"
docker-compose up -d --build

# Wait for services to be ready
echo -e "${YELLOW}Waiting for services to start...${NC}"
sleep 30

# Health check
echo -e "${YELLOW}Performing health checks...${NC}"
health_check() {
    local url=$1
    local name=$2
    
    for i in {1..10}; do
        if curl -f -s "$url/api/LoadTest/health" > /dev/null; then
            echo -e "${GREEN}✓ $name is healthy${NC}"
            return 0
        fi
        echo "Waiting for $name... ($i/10)"
        sleep 5
    done
    
    echo -e "${RED}✗ $name failed to start${NC}"
    return 1
}

health_check "http://localhost:8001" "Base API"
health_check "http://localhost:8002" "Primed API"

# Show initial thread pool stats
echo -e "${YELLOW}=== Initial Thread Pool Stats ===${NC}"
echo "Base API (not primed):"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

echo -e "\nPrimed API (before priming):"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

# Run different test scenarios
echo -e "${YELLOW}=== Running Load Tests ===${NC}"
echo -e "${GREEN}Phase 1: Testing Base API (Non-Primed) Only${NC}"

# Test 1: Base API - Fast operations only
echo -e "${GREEN}Test 1A: Base API - Fast operations (I/O bound)${NC}"
k6 run --duration 60s --vus 100 --console-output "base-1a.log" \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 100,
  duration: '60s',
  thresholds: {
    http_req_duration: ['p(95)<1000'],
    http_req_failed: ['rate<0.1'],
  },
};

export default function() {
  const response = http.get('http://localhost:8001/api/LoadTest/fast');

  check(response, {
    'base api fast status is 200': (r) => r.status === 200,
    'base api fast response time < 1000ms': (r) => r.timings.duration < 1000,
  });

  sleep(0.1);
}
EOF

echo -e "${YELLOW}Base API stats after fast operations test:${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

echo -e "${GREEN}Test 1B: Base API - CPU-bound operations${NC}"
k6 run --duration 60s --vus 20 --console-output "base-1b.log" \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 20,
  duration: '60s',
  thresholds: {
    http_req_duration: ['p(95)<10000'],
    http_req_failed: ['rate<0.1'],
  },
};

export default function() {
  const response = http.get('http://localhost:8001/api/LoadTest/cpu-bound');

  check(response, {
    'base api cpu status is 200': (r) => r.status === 200,
    'base api cpu response time < 10000ms': (r) => r.timings.duration < 10000,
  });

  sleep(1);
}
EOF

echo -e "${YELLOW}Base API stats after CPU-bound test:${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

echo -e "${GREEN}Test 1C: Base API - Mixed workload${NC}"
k6 run --duration 60s --vus 50 --console-output "base-1c.log" \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 50,
  duration: '60s',
  thresholds: {
    http_req_duration: ['p(95)<7500'],
    http_req_failed: ['rate<0.1'],
  },
};

export default function() {
  const endpoints = ['/api/LoadTest/fast', '/api/LoadTest/slow', '/api/LoadTest/cpu-bound', '/api/LoadTest/mixed'];
  const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
  const response = http.get(\`http://localhost:8001\${endpoint}\`);

  check(response, {
    'base api mixed status is 200': (r) => r.status === 200,
    'base api mixed response time < 7500ms': (r) => r.timings.duration < 7500,
  });

  sleep(0.3);
}
EOF

echo -e "${YELLOW}Final Base API stats:${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

# Wait for thread pool to settle before priming
echo -e "${YELLOW}Waiting 30 seconds for thread pools to settle...${NC}"
sleep 30

# Prime the primed API (simulating pod restart)
echo -e "${GREEN}Phase 2: Priming Thread Pool (Simulating Pod Restart)${NC}"
echo -e "${YELLOW}Priming the primed API thread pool...${NC}"
curl -s "http://localhost:8002/api/LoadTest/prime" | python3 -m json.tool

echo -e "${YELLOW}Primed API stats after priming:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

echo -e "${GREEN}Phase 3: Testing Primed API Only${NC}"

# Test 2: Primed API - Fast operations
echo -e "${GREEN}Test 2A: Primed API - Fast operations (I/O bound)${NC}"
k6 run --duration 60s --vus 100 --console-output "prime-2a.log" \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 100,
  duration: '60s',
  thresholds: {
    http_req_duration: ['p(95)<1000'],
    http_req_failed: ['rate<0.1'],
  },
};

export default function() {
  const response = http.get('http://localhost:8002/api/LoadTest/fast');

  check(response, {
    'primed api fast status is 200': (r) => r.status === 200,
    'primed api fast response time < 1000ms': (r) => r.timings.duration < 1000,
  });

  sleep(0.1);
}
EOF

echo -e "${YELLOW}Primed API stats after fast operations test:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

echo -e "${GREEN}Test 2B: Primed API - CPU-bound operations${NC}"
k6 run --duration 60s --vus 20 --console-output "prime-2b.log" \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 20,
  duration: '60s',
  thresholds: {
    http_req_duration: ['p(95)<10000'],
    http_req_failed: ['rate<0.1'],
  },
};

export default function() {
  const response = http.get('http://localhost:8002/api/LoadTest/cpu-bound');

  check(response, {
    'primed api cpu status is 200': (r) => r.status === 200,
    'primed api cpu response time < 10000ms': (r) => r.timings.duration < 10000,
  });

  sleep(1);
}
EOF

echo -e "${YELLOW}Primed API stats after CPU-bound test:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

echo -e "${GREEN}Test 2C: Primed API - Mixed workload${NC}"
k6 run --duration 60s --vus 50 --console-output "prime-2c.log" \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 50,
  duration: '60s',
  thresholds: {
    http_req_duration: ['p(95)<7500'],
    http_req_failed: ['rate<0.1'],
  },
};

export default function() {
  const endpoints = ['/api/LoadTest/fast', '/api/LoadTest/slow', '/api/LoadTest/cpu-bound', '/api/LoadTest/mixed'];
  const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
  const response = http.get(\`http://localhost:8002\${endpoint}\`);

  check(response, {
    'primed api mixed status is 200': (r) => r.status === 200,
    'primed api mixed response time < 7500ms': (r) => r.timings.duration < 7500,
  });

  sleep(0.3);
}
EOF

echo -e "${YELLOW}Final Primed API stats:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

echo -e "${GREEN}Phase 4: Advanced Load Tests${NC}"
echo -e "${GREEN}Test 3: Concurrent comparison using separate scenarios${NC}"
k6 run load-tests.js

# Show final thread pool stats comparison
echo -e "${YELLOW}=== Final Thread Pool Stats Comparison ===${NC}"
echo -e "${GREEN}Base API (Non-Primed) Final Stats:${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

echo -e "\n${GREEN}Primed API Final Stats:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

# Show container logs for analysis
echo -e "${YELLOW}=== Container Logs (last 50 lines) ===${NC}"
echo -e "${GREEN}Base API logs:${NC}"
docker-compose logs --tail=50 threadpool-demo

echo -e "${GREEN}Primed API logs:${NC}"
docker-compose logs --tail=50 threadpool-demo-primed

echo -e "${GREEN}=== Test Summary ===${NC}"
echo "Phase 1 - Base API Tests (Non-Primed):"
echo "  1A. Fast operations test completed - Baseline I/O performance"
echo "  1B. CPU-bound operations test completed - Baseline CPU performance"
echo "  1C. Mixed workload test completed - Baseline mixed performance"
echo ""
echo "Phase 2 - Thread Pool Priming:"
echo "  - Primed API thread pool warmed up (simulating pod restart)"
echo ""
echo "Phase 3 - Primed API Tests:"
echo "  2A. Fast operations test completed - Primed I/O performance"
echo "  2B. CPU-bound operations test completed - Primed CPU performance"
echo "  2C. Mixed workload test completed - Primed mixed performance"
echo ""
echo "Phase 4 - Advanced Tests:"
echo "  3. Concurrent comparison test completed - Side-by-side comparison"
echo ""
echo "Key Comparison Points:"
echo "- Base API (http://localhost:8001) - Cold start, no thread pool priming"
echo "- Primed API (http://localhost:8002) - Warmed up thread pool"
echo ""
echo "Metrics to Compare:"
echo "- Response times (p95, p99) - Should be lower for primed API"
echo "- Thread pool utilization - Primed should have better patterns"
echo "- Error rates - Primed should have fewer timeouts/failures"
echo "- Ramp-up performance - Primed should handle load spikes better"

# Cleanup function
cleanup() {
    echo -e "${YELLOW}Cleaning up...${NC}"
    docker-compose down
    echo -e "${GREEN}Cleanup completed${NC}"
}

# Ask user if they want to keep containers running
read -p "Keep containers running for manual testing? (y/N): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    cleanup
else
    echo -e "${GREEN}Containers are still running. Use 'docker-compose down' to stop them.${NC}"
    echo -e "${YELLOW}Access the APIs at:${NC}"
    echo "- Base API: http://localhost:8001"
    echo "- Primed API: http://localhost:8002"
    echo "- Swagger UI: http://localhost:8001/swagger (if in development mode)"
fi