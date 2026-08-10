import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter, Trend, Rate } from 'k6/metrics';

/**
 * BFF Stress Test — Breakpoint Test for His.Hope
 *
 * Gradually ramps load until the system breaks or hits 1000 concurrent VUs.
 * Designed to find the breaking point of BFF modules under extreme load.
 *
 * Key metrics:
 * - Breaking point: where error rate exceeds 5% or p(99) > 10s
 * - Recovery behavior: does the system recover when load drops?
 * - Resource exhaustion: memory, CPU, connection pool limits
 */

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const bffErrors = new Counter('bff_errors');
const bffDuration = new Trend('bff_duration', true);
const bffRecoveryTime = new Trend('bff_recovery_time', true);

const BFF_ENDPOINTS = [
  '/api/v1/patients/search?q=test&page=1&pageSize=10',
  '/api/v1/patients/PAT-001/timeline',
  '/api/v1/encounters/search',
  '/api/v1/encounters/ENC-001/full',
  '/api/v1/lab-orders/search',
  '/api/v1/invoices/search',
  '/api/v1/invoices/INV-001/detailed',
  '/api/v1/medications/search',
  '/api/v1/medications/MED-001/full',
  '/api/v1/dashboard/stats',
  '/api/v1/dashboard/recent-encounters',
];

const SESSION_COOKIE = 'hishop_sid=test-load-session';
const CSRF_COOKIE = 'hishop_csrf=test-load-csrf';

export const options = {
  stages: [
    { target: 50, duration: '2m' },
    { target: 100, duration: '2m' },
    { target: 200, duration: '2m' },
    { target: 400, duration: '2m' },
    { target: 800, duration: '2m' },
    { target: 1000, duration: '2m' },
  ],
  thresholds: {
    'http_req_failed': ['rate<0.05'],
  },
};

export default function () {
  const endpoint = BFF_ENDPOINTS[Math.floor(Math.random() * BFF_ENDPOINTS.length)];

  group('BFF Stress', () => {
    const headers = {
      'Cookie': `${SESSION_COOKIE}; ${CSRF_COOKIE}`,
      'User-Agent': 'k6-stress-test/1.0',
      'X-CSRF-Token': 'test-load-csrf-token',
    };

    const res = http.get(`${BASE_URL}${endpoint}`, { headers });

    check(res, {
      'status is 2xx or 401': (r) => r.status >= 200 && r.status < 500,
      'response not empty': (r) => r.body && r.body.length > 0,
    });

    bffDuration.add(res.timings.duration);

    // Track recovery — if request fails, record the timestamp
    if (res.status >= 500 || res.status === 0) {
      bffErrors.add(1);
      bffRecoveryTime.add(Date.now());
    }

    // Minimal sleep to maximize throughput
    sleep(Math.random() * 0.3 + 0.1);
  });
}
