#!/bin/bash

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== Isolated Thread Pool Comparison Test ===${NC}"
echo "This script runs completely isolated tests to demonstrate thread pool priming benefits"

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

# Check if services are running
echo -e "${YELLOW}Checking service health...${NC}"
BASE_HEALTH=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:8001/api/LoadTest/health")
PRIMED_HEALTH=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:8002/api/LoadTest/health")

if [ "$BASE_HEALTH" != "200" ] || [ "$PRIMED_HEALTH" != "200" ]; then
    echo -e "${RED}Services not ready. Base: $BASE_HEALTH, Primed: $PRIMED_HEALTH${NC}"
    echo "Please run 'docker-compose up -d --build' first"
    exit 1
fi

echo -e "${GREEN}Services are healthy${NC}"

# Show initial thread pool stats
echo -e "${YELLOW}=== Initial Thread Pool Stats ===${NC}"
echo -e "${GREEN}Base API (not primed):${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

echo -e "\n${GREEN}Primed API (before priming):${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

# Phase 1: Test Base API Only
echo -e "\n${YELLOW}=== Phase 1: Testing Base API Only (60 seconds) ===${NC}"
k6 run --duration 60s --vus 20 \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 20,
  duration: '60s',
  thresholds: {
    http_req_duration: ['p(95)<3000'],
    http_req_failed: ['rate<0.1'],
  },
};

export default function() {
  // Mix of different operations to stress the thread pool
  const operations = [
    { endpoint: '/api/LoadTest/fast', weight: 0.4, sleep: 0.1 },
    { endpoint: '/api/LoadTest/slow', weight: 0.3, sleep: 0.5 },
    { endpoint: '/api/LoadTest/cpu-bound', weight: 0.2, sleep: 1.0 },
    { endpoint: '/api/LoadTest/mixed', weight: 0.1, sleep: 0.3 }
  ];
  
  const rand = Math.random();
  let cumulative = 0;
  let selectedOp = operations[0];
  
  for (const op of operations) {
    cumulative += op.weight;
    if (rand <= cumulative) {
      selectedOp = op;
      break;
    }
  }
  
  const response = http.get(\`http://localhost:8001\${selectedOp.endpoint}\`);
  
  check(response, {
    'base api status is 200': (r) => r.status === 200,
    'base api response time < 5000ms': (r) => r.timings.duration < 5000,
  });
  
  sleep(selectedOp.sleep);
}
EOF

echo -e "\n${YELLOW}Base API stats after load test:${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

# Wait for thread pools to settle
echo -e "\n${YELLOW}Waiting 30 seconds for thread pools to settle...${NC}"
sleep 30

# Phase 2: Prime the primed API (simulating pod restart)
echo -e "\n${YELLOW}=== Phase 2: Priming Thread Pool (Simulating Pod Restart) ===${NC}"
echo -e "${YELLOW}Priming the primed API thread pool...${NC}"
PRIME_RESPONSE=$(curl -s "http://localhost:8002/api/LoadTest/prime")
echo "Prime response: $PRIME_RESPONSE"

echo -e "\n${YELLOW}Primed API stats after priming:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

# Wait a moment for priming to complete
sleep 5

# Phase 3: Test Primed API Only
echo -e "\n${YELLOW}=== Phase 3: Testing Primed API Only (60 seconds) ===${NC}"
k6 run --duration 60s --vus 20 \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 20,
  duration: '60s',
  thresholds: {
    http_req_duration: ['p(95)<3000'],
    http_req_failed: ['rate<0.1'],
  },
};

export default function() {
  // Same mix as base API for fair comparison
  const operations = [
    { endpoint: '/api/LoadTest/fast', weight: 0.4, sleep: 0.1 },
    { endpoint: '/api/LoadTest/slow', weight: 0.3, sleep: 0.5 },
    { endpoint: '/api/LoadTest/cpu-bound', weight: 0.2, sleep: 1.0 },
    { endpoint: '/api/LoadTest/mixed', weight: 0.1, sleep: 0.3 }
  ];
  
  const rand = Math.random();
  let cumulative = 0;
  let selectedOp = operations[0];
  
  for (const op of operations) {
    cumulative += op.weight;
    if (rand <= cumulative) {
      selectedOp = op;
      break;
    }
  }
  
  const response = http.get(\`http://localhost:8002\${selectedOp.endpoint}\`);
  
  check(response, {
    'primed api status is 200': (r) => r.status === 200,
    'primed api response time < 5000ms': (r) => r.timings.duration < 5000,
  });
  
  sleep(selectedOp.sleep);
}
EOF

echo -e "\n${YELLOW}Primed API stats after load test:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

# Final comparison
echo -e "\n${YELLOW}=== Final Thread Pool Stats Comparison ===${NC}"
echo -e "${GREEN}Base API (Non-Primed) Final Stats:${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

echo -e "\n${GREEN}Primed API Final Stats:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

echo -e "\n${GREEN}=== Isolated Test Complete ===${NC}"
echo "This test demonstrates the benefits of thread pool priming by:"
echo "1. Testing base API in complete isolation"
echo "2. Priming the thread pool (simulating pod restart)"
echo "3. Testing primed API in complete isolation"
echo "4. Comparing final results"
echo ""
echo "Key metrics to compare:"
echo "- Response times (p95, p99) - Should be better for primed API"
echo "- Thread pool utilization - Primed should show better patterns"
echo "- Error rates - Primed should have fewer failures"
echo "- Ramp-up performance - Primed should handle initial load better"
