import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

// Test configuration
export const options = {
  scenarios: {
    // Base API (Non-primed) Scenarios
    base_fast_operations: {
      executor: 'ramping-vus',
      exec: 'baseFastOperationTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 50 },
        { duration: '60s', target: 100 },
        { duration: '30s', target: 0 },
      ],
    },

    base_slow_operations: {
      executor: 'ramping-vus',
      exec: 'baseSlowOperationTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 25 },
        { duration: '60s', target: 50 },
        { duration: '30s', target: 0 },
      ],
    },

    base_cpu_bound_operations: {
      executor: 'ramping-vus',
      exec: 'baseCpuBoundTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 5 },
        { duration: '60s', target: 10 },
        { duration: '30s', target: 0 },
      ],
    },

    base_mixed_operations: {
      executor: 'ramping-vus',
      exec: 'baseMixedOperationTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 15 },
        { duration: '60s', target: 30 },
        { duration: '30s', target: 0 },
      ],
    },

    base_blocking_operations: {
      executor: 'ramping-vus',
      exec: 'baseBlockingOperationTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 3 },
        { duration: '60s', target: 5 },
        { duration: '30s', target: 0 },
      ],
    },

    // Primed API Scenarios
    primed_fast_operations: {
      executor: 'ramping-vus',
      exec: 'primedFastOperationTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 50 },
        { duration: '60s', target: 100 },
        { duration: '30s', target: 0 },
      ],
    },

    primed_slow_operations: {
      executor: 'ramping-vus',
      exec: 'primedSlowOperationTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 25 },
        { duration: '60s', target: 50 },
        { duration: '30s', target: 0 },
      ],
    },

    primed_cpu_bound_operations: {
      executor: 'ramping-vus',
      exec: 'primedCpuBoundTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 5 },
        { duration: '60s', target: 10 },
        { duration: '30s', target: 0 },
      ],
    },

    primed_mixed_operations: {
      executor: 'ramping-vus',
      exec: 'primedMixedOperationTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 8 },
        { duration: '60s', target: 15 },
        { duration: '30s', target: 0 },
      ],
    },

    primed_blocking_operations: {
      executor: 'ramping-vus',
      exec: 'primedBlockingOperationTest',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 3 },
        { duration: '60s', target: 5 },
        { duration: '30s', target: 0 },
      ],
    },
  },
  
  thresholds: {
    http_req_duration: ['p(95)<5000'], // 95% of requests should be below 5s
    http_req_failed: ['rate<0.1'], // Error rate should be below 10%
  },
};

// Base URLs for comparison
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8001';
const PRIMED_URL = __ENV.PRIMED_URL || 'http://localhost:8002';

// Base API Test functions
export function baseFastOperationTest() {
  const response = http.get(`${BASE_URL}/api/LoadTest/fast`);

  const success = check(response, {
    'base fast operation status is 200': (r) => r.status === 200,
    'base fast operation response time < 1000ms': (r) => r.timings.duration < 1000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Base fast operation failed: ${response.status} - ${response.body}`);
  }

  sleep(0.1);
}

export function baseSlowOperationTest() {
  const response = http.get(`${BASE_URL}/api/LoadTest/slow`);

  const success = check(response, {
    'base slow operation status is 200': (r) => r.status === 200,
    'base slow operation response time < 5000ms': (r) => r.timings.duration < 5000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Base slow operation failed: ${response.status} - ${response.body}`);
  }

  sleep(0.5);
}

export function baseCpuBoundTest() {
  const response = http.get(`${BASE_URL}/api/LoadTest/cpu-bound`);

  const success = check(response, {
    'base CPU-bound operation status is 200': (r) => r.status === 200,
    'base CPU-bound operation response time < 10000ms': (r) => r.timings.duration < 10000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Base CPU-bound operation failed: ${response.status} - ${response.body}`);
  }

  sleep(1);
}

export function baseMixedOperationTest() {
  const response = http.get(`${BASE_URL}/api/LoadTest/mixed`);

  const success = check(response, {
    'base mixed operation status is 200': (r) => r.status === 200,
    'base mixed operation response time < 3000ms': (r) => r.timings.duration < 3000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Base mixed operation failed: ${response.status} - ${response.body}`);
  }

  sleep(0.3);
}

export function baseBlockingOperationTest() {
  const response = http.get(`${BASE_URL}/api/LoadTest/blocking`);

  const success = check(response, {
    'base blocking operation status is 200': (r) => r.status === 200,
    'base blocking operation response time < 15000ms': (r) => r.timings.duration < 15000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Base blocking operation failed: ${response.status} - ${response.body}`);
  }

  sleep(2);
}

// Primed API Test functions
export function primedFastOperationTest() {
  const response = http.get(`${PRIMED_URL}/api/LoadTest/fast`);

  const success = check(response, {
    'primed fast operation status is 200': (r) => r.status === 200,
    'primed fast operation response time < 1000ms': (r) => r.timings.duration < 1000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Primed fast operation failed: ${response.status} - ${response.body}`);
  }

  sleep(0.1);
}

export function primedSlowOperationTest() {
  const response = http.get(`${PRIMED_URL}/api/LoadTest/slow`);

  const success = check(response, {
    'primed slow operation status is 200': (r) => r.status === 200,
    'primed slow operation response time < 5000ms': (r) => r.timings.duration < 5000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Primed slow operation failed: ${response.status} - ${response.body}`);
  }

  sleep(0.5);
}

export function primedCpuBoundTest() {
  const response = http.get(`${PRIMED_URL}/api/LoadTest/cpu-bound`);

  const success = check(response, {
    'primed CPU-bound operation status is 200': (r) => r.status === 200,
    'primed CPU-bound operation response time < 10000ms': (r) => r.timings.duration < 10000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Primed CPU-bound operation failed: ${response.status} - ${response.body}`);
  }

  sleep(1);
}

export function primedMixedOperationTest() {
  const response = http.get(`${PRIMED_URL}/api/LoadTest/mixed`);

  const success = check(response, {
    'primed mixed operation status is 200': (r) => r.status === 200,
    'primed mixed operation response time < 3000ms': (r) => r.timings.duration < 3000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Primed mixed operation failed: ${response.status} - ${response.body}`);
  }

  sleep(0.3);
}

export function primedBlockingOperationTest() {
  const response = http.get(`${PRIMED_URL}/api/LoadTest/blocking`);

  const success = check(response, {
    'primed blocking operation status is 200': (r) => r.status === 200,
    'primed blocking operation response time < 15000ms': (r) => r.timings.duration < 15000,
  });

  errorRate.add(!success);

  if (!success) {
    console.log(`Primed blocking operation failed: ${response.status} - ${response.body}`);
  }

  sleep(2);
}



// Health check function
export function healthCheck() {
  const response = http.get(`${BASE_URL}/api/LoadTest/health`);
  check(response, {
    'health check status is 200': (r) => r.status === 200,
  });
}

// Thread pool stats monitoring
export function monitorThreadPool() {
  const baseStats = http.get(`${BASE_URL}/api/LoadTest/stats`);
  const primedStats = http.get(`${PRIMED_URL}/api/LoadTest/stats`);
  
  if (baseStats.status === 200 && primedStats.status === 200) {
    const baseData = JSON.parse(baseStats.body);
    const primedData = JSON.parse(primedStats.body);
    
    console.log('=== Thread Pool Comparison ===');
    console.log(`Base API - Active Worker Threads: ${baseData.ActiveWorkerThreads}/${baseData.MaxWorkerThreads}`);
    console.log(`Primed API - Active Worker Threads: ${primedData.ActiveWorkerThreads}/${primedData.MaxWorkerThreads}`);
    console.log(`Base API - Active IOCP Threads: ${baseData.ActiveCompletionPortThreads}/${baseData.MaxCompletionPortThreads}`);
    console.log(`Primed API - Active IOCP Threads: ${primedData.ActiveCompletionPortThreads}/${primedData.MaxCompletionPortThreads}`);
  }
}

// Setup function to run before tests
export function setup() {
  // Wait for services to be ready
  sleep(5);
  
  // Prime one of the thread pools
  const primeResponse = http.post(`${PRIMED_URL}/api/LoadTest/prime`);
  console.log(`Thread pool priming response: ${primeResponse.status}`);
  
  return {
    baseUrl: BASE_URL,
    primedUrl: PRIMED_URL
  };
}

// Teardown function
export function teardown(data) {
  console.log('Load test completed');
  console.log(`Base URL: ${data.baseUrl}`);
  console.log(`Primed URL: ${data.primedUrl}`);
}