import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

// Base URLs for comparison
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8001';
const PRIMED_URL = __ENV.PRIMED_URL || 'http://localhost:8002';

// Test configuration - Sequential isolated tests
export const options = {
  scenarios: {
    // Phase 1: Test Base API only
    phase1_base_warmup: {
      executor: 'ramping-vus',
      exec: 'baseWarmupTest',
      startTime: '0s',
      startVUs: 0,
      stages: [
        { duration: '10s', target: 5 },
        { duration: '20s', target: 5 },
        { duration: '10s', target: 0 },
      ],
    },
    
    phase2_base_load: {
      executor: 'ramping-vus',
      exec: 'baseLoadTest',
      startTime: '45s',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 20 },
        { duration: '60s', target: 40 },
        { duration: '30s', target: 0 },
      ],
    },
    
    // Phase 2: Test Primed API only (after base API test completes)
    phase3_primed_warmup: {
      executor: 'ramping-vus',
      exec: 'primedWarmupTest',
      startTime: '180s',
      startVUs: 0,
      stages: [
        { duration: '10s', target: 5 },
        { duration: '20s', target: 5 },
        { duration: '10s', target: 0 },
      ],
    },
    
    phase4_primed_load: {
      executor: 'ramping-vus',
      exec: 'primedLoadTest',
      startTime: '225s',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 20 },
        { duration: '60s', target: 40 },
        { duration: '30s', target: 0 },
      ],
    },
    
    // Phase 3: Direct comparison with identical load patterns
    phase5_comparison_base: {
      executor: 'ramping-vus',
      exec: 'comparisonBaseTest',
      startTime: '360s',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 15 },
        { duration: '40s', target: 30 },
        { duration: '20s', target: 0 },
      ],
    },
    
    phase6_comparison_primed: {
      executor: 'ramping-vus',
      exec: 'comparisonPrimedTest',
      startTime: '360s',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 15 },
        { duration: '40s', target: 30 },
        { duration: '20s', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<5000'],
    http_req_failed: ['rate<0.1'],
    errors: ['rate<0.1'],
  },
};

// Base API test functions
export function baseWarmupTest() {
  const endpoints = ['/api/LoadTest/fast', '/api/LoadTest/slow', '/api/LoadTest/cpu-bound'];
  const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
  
  const response = http.get(`${BASE_URL}${endpoint}`);
  
  const success = check(response, {
    'base warmup status is 200': (r) => r.status === 200,
    'base warmup response time < 3000ms': (r) => r.timings.duration < 3000,
  });
  
  errorRate.add(!success);
  sleep(0.2);
}

export function baseLoadTest() {
  // Mix of different operation types to stress the thread pool
  const operations = [
    { endpoint: '/api/LoadTest/fast', weight: 0.4, sleep: 0.1 },
    { endpoint: '/api/LoadTest/slow', weight: 0.3, sleep: 0.5 },
    { endpoint: '/api/LoadTest/cpu-bound', weight: 0.2, sleep: 1.0 },
    { endpoint: '/api/LoadTest/blocking', weight: 0.1, sleep: 2.0 }
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
  
  const response = http.get(`${BASE_URL}${selectedOp.endpoint}`);
  
  const success = check(response, {
    'base load test status is 200': (r) => r.status === 200,
    'base load test response time < 10000ms': (r) => r.timings.duration < 10000,
  });
  
  errorRate.add(!success);
  
  if (!success) {
    console.log(`Base load test failed: ${selectedOp.endpoint} - ${response.status}`);
  }
  
  sleep(selectedOp.sleep);
}

// Primed API test functions
export function primedWarmupTest() {
  const endpoints = ['/api/LoadTest/fast', '/api/LoadTest/slow', '/api/LoadTest/cpu-bound'];
  const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
  
  const response = http.get(`${PRIMED_URL}${endpoint}`);
  
  const success = check(response, {
    'primed warmup status is 200': (r) => r.status === 200,
    'primed warmup response time < 3000ms': (r) => r.timings.duration < 3000,
  });
  
  errorRate.add(!success);
  sleep(0.2);
}

export function primedLoadTest() {
  // Same mix as base API for fair comparison
  const operations = [
    { endpoint: '/api/LoadTest/fast', weight: 0.4, sleep: 0.1 },
    { endpoint: '/api/LoadTest/slow', weight: 0.3, sleep: 0.5 },
    { endpoint: '/api/LoadTest/cpu-bound', weight: 0.2, sleep: 1.0 },
    { endpoint: '/api/LoadTest/blocking', weight: 0.1, sleep: 2.0 }
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
  
  const response = http.get(`${PRIMED_URL}${selectedOp.endpoint}`);
  
  const success = check(response, {
    'primed load test status is 200': (r) => r.status === 200,
    'primed load test response time < 10000ms': (r) => r.timings.duration < 10000,
  });
  
  errorRate.add(!success);
  
  if (!success) {
    console.log(`Primed load test failed: ${selectedOp.endpoint} - ${response.status}`);
  }
  
  sleep(selectedOp.sleep);
}

// Direct comparison tests (run simultaneously)
export function comparisonBaseTest() {
  const response = http.get(`${BASE_URL}/api/LoadTest/mixed`);
  
  const success = check(response, {
    'comparison base status is 200': (r) => r.status === 200,
    'comparison base response time < 5000ms': (r) => r.timings.duration < 5000,
  });
  
  errorRate.add(!success);
  sleep(0.3);
}

export function comparisonPrimedTest() {
  const response = http.get(`${PRIMED_URL}/api/LoadTest/mixed`);
  
  const success = check(response, {
    'comparison primed status is 200': (r) => r.status === 200,
    'comparison primed response time < 5000ms': (r) => r.timings.duration < 5000,
  });
  
  errorRate.add(!success);
  sleep(0.3);
}

// Setup function to run before tests
export function setup() {
  console.log('=== Starting Isolated Load Test ===');
  console.log(`Base URL: ${BASE_URL}`);
  console.log(`Primed URL: ${PRIMED_URL}`);
  
  // Wait for services to be ready
  sleep(5);
  
  // Prime the thread pool on the primed service
  const primeResponse = http.post(`${PRIMED_URL}/api/LoadTest/prime`);
  console.log(`Thread pool priming response: ${primeResponse.status}`);
  
  // Get initial stats
  const baseStats = http.get(`${BASE_URL}/api/LoadTest/stats`);
  const primedStats = http.get(`${PRIMED_URL}/api/LoadTest/stats`);
  
  if (baseStats.status === 200 && primedStats.status === 200) {
    console.log('=== Initial Thread Pool Stats ===');
    console.log(`Base API: ${baseStats.body}`);
    console.log(`Primed API: ${primedStats.body}`);
  }
  
  return {
    baseUrl: BASE_URL,
    primedUrl: PRIMED_URL
  };
}

// Teardown function
export function teardown(data) {
  console.log('=== Load Test Completed ===');
  
  // Get final stats
  const baseStats = http.get(`${data.baseUrl}/api/LoadTest/stats`);
  const primedStats = http.get(`${data.primedUrl}/api/LoadTest/stats`);
  
  if (baseStats.status === 200 && primedStats.status === 200) {
    console.log('=== Final Thread Pool Stats ===');
    console.log(`Base API: ${baseStats.body}`);
    console.log(`Primed API: ${primedStats.body}`);
  }
}
