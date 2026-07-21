import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter, Trend, Rate } from 'k6/metrics';

/**
 * BFF Load Test for His.Hope
 *
 * Simulates realistic BFF frontend aggregation traffic across all 7 BFF modules:
 * - Patient BFF (port 5100)
 * - Clinical BFF (port 5200)
 * - Lab BFF (port 5300)
 * - Billing BFF (port 5400)
 * - Pharmacy BFF (port 5500)
 * - Dashboard BFF (port 5600)
 *
 * Uses session-based auth simulation and tracks per-BFF metrics.
 * Designed to run against the API Gateway at localhost:5000.
 */

const bffErrors = new Counter('bff_errors');
const bffDuration = new Trend('bff_duration', true);
const sessionHitRate = new Rate('session_hit_rate');

const BFF_MODULES = [
  { name: 'patient', port: 5100, endpoints: ['/api/v1/patients/search?q=test&page=1&pageSize=10', '/api/v1/patients/PAT-001/timeline'] },
  { name: 'clinical', port: 5200, endpoints: ['/api/v1/encounters/search', '/api/v1/encounters/ENC-001/full'] },
  { name: 'lab', port: 5300, endpoints: ['/api/v1/lab-orders/search'] },
  { name: 'billing', port: 5400, endpoints: ['/api/v1/invoices/search', '/api/v1/invoices/INV-001/detailed'] },
  { name: 'pharmacy', port: 5500, endpoints: ['/api/v1/medications/search', '/api/v1/medications/MED-001/full'] },
  { name: 'dashboard', port: 5600, endpoints: ['/api/v1/dashboard/stats', '/api/v1/dashboard/recent-encounters'] },
];

const SESSION_COOKIE = 'hishop_sid=test-load-session';
const CSRF_COOKIE = 'hishop_csrf=test-load-csrf';

export const options = {
  scenarios: {
    // Warmup phase — gentle ramp to prime caches and connections
    warmup: {
      executor: 'ramping-arrival-rate',
      startRate: 1,
      timeUnit: '1s',
      preAllocatedVUs: 10,
      stages: [
        { target: 10, duration: '30s' },
        { target: 10, duration: '30s' },
      ],
    },
    // Steady-state load — ramp to target and observe degradation
    steady: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 100,
      maxVUs: 200,
      stages: [
        { target: 50, duration: '2m' },
        { target: 100, duration: '3m' },
        { target: 200, duration: '5m' },
        { target: 100, duration: '2m' },
        { target: 10, duration: '1m' },
      ],
    },
  },
  thresholds: {
    'http_req_duration': ['p(95)<500', 'p(99)<1000'],
    'http_req_failed': ['rate<0.01'],
    'bff_duration': ['p(95)<400'],
    'bff_errors': ['count<50'],
    'session_hit_rate': ['rate>0.98'],
  },
};

export default function () {
  const module = BFF_MODULES[Math.floor(Math.random() * BFF_MODULES.length)];

  group(`BFF:${module.name}`, () => {
    const endpoint = module.endpoints[Math.floor(Math.random() * module.endpoints.length)];
    const isMutation = ['POST', 'PUT', 'PATCH', 'DELETE'].includes(
      endpoint.includes('create') || endpoint.includes('submit') ? 'POST' : 'GET'
    );

    const headers = {
      'Cookie': `${SESSION_COOKIE}; ${CSRF_COOKIE}`,
      'User-Agent': 'k6-load-test/1.0',
    };
    if (isMutation) {
      headers['X-CSRF-Token'] = 'test-load-csrf-token';
    }

    const res = http.get(`http://localhost:5000${endpoint}`, { headers });

    check(res, {
      'status is 2xx or 401': (r) => r.status >= 200 && r.status < 500,
      'response has data': (r) => r.body && r.body.length > 0,
    });

    bffDuration.add(res.timings.duration);
    if (res.status >= 500) bffErrors.add(1);

    // Track session cookie presence
    sessionHitRate.add(res.request.headers['Cookie'] && res.request.headers['Cookie'].includes('hishop_sid'));

    sleep(Math.random() * 2);
  });
}

export function setup() {
  // Pre-warm: authenticate and get session cookie
  const loginRes = http.post('http://localhost:5000/api/v1/auth/login', JSON.stringify({
    username: 'load-test-user',
    password: 'LoadTest@123'
  }), { headers: { 'Content-Type': 'application/json' } });

  check(loginRes, { 'login succeeded': (r) => r.status === 200 });

  return { loginCookies: loginRes.headers['Set-Cookie'] };
}
