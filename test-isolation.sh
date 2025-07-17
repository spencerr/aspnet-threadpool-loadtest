#!/bin/bash

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== Thread Pool Isolation Test ===${NC}"

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

# Prime the primed service
echo -e "${YELLOW}Priming the thread pool on primed service...${NC}"
PRIME_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "http://localhost:8002/api/LoadTest/prime")
echo "Prime response: $PRIME_RESPONSE"

# Wait a moment for priming to complete
sleep 2

# Get initial thread pool stats
echo -e "${YELLOW}=== Initial Thread Pool Stats ===${NC}"
echo -e "${GREEN}Base API (not primed):${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

echo -e "\n${GREEN}Primed API:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

# Test 1: Base API only
echo -e "\n${YELLOW}=== Test 1: Base API Load (30 seconds) ===${NC}"
k6 run --duration 30s --vus 10 -e BASE_URL=http://localhost:8001 -e PRIMED_URL=http://localhost:8002 \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
};

export default function() {
  const response = http.get('http://localhost:8001/api/LoadTest/mixed');
  
  check(response, {
    'base api status is 200': (r) => r.status === 200,
    'base api response time < 3000ms': (r) => r.timings.duration < 3000,
  });
  
  sleep(0.3);
}
EOF

echo -e "\n${YELLOW}Base API stats after load:${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

# Wait for thread pool to settle
echo -e "\n${YELLOW}Waiting 10 seconds for thread pools to settle...${NC}"
sleep 10

# Test 2: Primed API only
echo -e "\n${YELLOW}=== Test 2: Primed API Load (30 seconds) ===${NC}"
k6 run --duration 30s --vus 10 -e BASE_URL=http://localhost:8001 -e PRIMED_URL=http://localhost:8002 \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
};

export default function() {
  const response = http.get('http://localhost:8002/api/LoadTest/mixed');
  
  check(response, {
    'primed api status is 200': (r) => r.status === 200,
    'primed api response time < 3000ms': (r) => r.timings.duration < 3000,
  });
  
  sleep(0.3);
}
EOF

echo -e "\n${YELLOW}Primed API stats after load:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

# Wait for thread pool to settle
echo -e "\n${YELLOW}Waiting 10 seconds for thread pools to settle...${NC}"
sleep 10

# Test 3: Side-by-side comparison
echo -e "\n${YELLOW}=== Test 3: Side-by-side Comparison (30 seconds) ===${NC}"
k6 run --duration 30s --vus 20 -e BASE_URL=http://localhost:8001 -e PRIMED_URL=http://localhost:8002 \
    --summary-trend-stats="avg,min,med,max,p(95),p(99)" \
    - <<EOF
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 20,
  duration: '30s',
};

export default function() {
  // Alternate between base and primed APIs
  const useBase = __VU % 2 === 0;
  const url = useBase ? 'http://localhost:8001' : 'http://localhost:8002';
  const label = useBase ? 'base' : 'primed';
  
  const response = http.get(\`\${url}/api/LoadTest/mixed\`);
  
  check(response, {
    [\`\${label} api status is 200\`]: (r) => r.status === 200,
    [\`\${label} api response time < 3000ms\`]: (r) => r.timings.duration < 3000,
  });
  
  sleep(0.3);
}
EOF

# Final stats comparison
echo -e "\n${YELLOW}=== Final Thread Pool Stats Comparison ===${NC}"
echo -e "${GREEN}Base API (not primed):${NC}"
curl -s "http://localhost:8001/api/LoadTest/stats" | python3 -m json.tool

echo -e "\n${GREEN}Primed API:${NC}"
curl -s "http://localhost:8002/api/LoadTest/stats" | python3 -m json.tool

echo -e "\n${GREEN}=== Isolation Test Complete ===${NC}"
echo "Review the results above to see the difference between primed and non-primed thread pools."
echo "Key metrics to compare:"
echo "- Response times (p95, p99)"
echo "- Thread pool utilization"
echo "- Active vs Available threads"
