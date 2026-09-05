import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

// Authenticated Identity control-plane profile. It intentionally exercises
// tenant discovery and both durable outbox health surfaces; no selector is
// accepted in the URL or request body.
const baseUrl = __ENV.IDENTITY_BASE_URL || 'http://localhost:5000';
const token = __ENV.AUTH_TOKEN || '';
const target = Number(__ENV.LOAD_TARGET || 200);
const duration = __ENV.LOAD_DURATION || '5m';
const latency = new Trend('identity_control_plane_latency', true);

export const options = {
  stages: [
    { duration: '1m', target: Math.max(1, Math.floor(target / 5)) },
    { duration, target },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    identity_control_plane_latency: ['p(95)<750', 'p(99)<1500'],
  },
};

export function setup() {
  if (!token) throw new Error('AUTH_TOKEN is required for the Identity scale profile.');
}

export default function () {
  const headers = { Authorization: `Bearer ${token}` };
  for (const path of [
    '/api/v1/admin/me/switchable-tenants',
    '/api/v1/admin/provisioning/delivery-health',
    '/api/v1/admin/security-signals/status',
    '/api/v1/admin/security-signals/outbox',
  ]) {
    const response = http.get(`${baseUrl}${path}`, { headers, tags: { workload: 'identity-scale' } });
    latency.add(response.timings.duration);
    check(response, { [`${path} status 2xx`]: r => r.status >= 200 && r.status < 300 });
  }
  sleep(0.5);
}
