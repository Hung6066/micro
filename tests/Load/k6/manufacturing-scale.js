import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

// Enterprise tenant-scale profile. Run against a seeded environment with an
// authenticated operator token and a tenant that has representative outbox
// backlog. The tenant is carried only in the canonical context header.
const baseUrl = __ENV.BASE_URL || 'http://localhost:5050';
const token = __ENV.AUTH_TOKEN || '';
const tenant = __ENV.TENANT_KEY || '';
const target = Number(__ENV.LOAD_TARGET || 500);
const duration = __ENV.LOAD_DURATION || '5m';
const latency = new Trend('manufacturing_read_latency', true);

export const options = {
  stages: [
    { duration: '1m', target: Math.max(1, Math.floor(target / 5)) },
    { duration, target },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    manufacturing_read_latency: ['p(95)<750', 'p(99)<1500'],
  },
};

export function setup() {
  if (!token) throw new Error('AUTH_TOKEN is required for the manufacturing scale profile.');
  if (!tenant) throw new Error('TENANT_KEY is required; refusing an unscoped tenant load.');
}

export default function () {
  const headers = {
    Authorization: `Bearer ${token}`,
    'X-HisHope-Tenant': tenant,
  };
  for (const path of [
    '/api/v1/manufacturing/dashboard/manufacturing-summary',
    '/api/v1/manufacturing/production-orders?limit=50',
    '/api/v1/manufacturing/events/receipts?limit=50',
  ]) {
    const response = http.get(`${baseUrl}${path}`, { headers, tags: { workload: 'manufacturing-scale' } });
    latency.add(response.timings.duration);
    check(response, { [`${path} status 200`]: r => r.status === 200 });
  }
  sleep(0.5);
}
