import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const baseUrl = __ENV.BASE_URL || 'http://localhost:5000/api/v1';
const token = __ENV.AUTH_TOKEN || '';
const duration = __ENV.LOAD_DURATION || '2m';
const target = Number(__ENV.LOAD_TARGET || 50);
const queryDuration = new Trend('database_read_duration', true);

export const options = {
  stages: [
    { duration: '30s', target: Math.max(1, Math.floor(target / 2)) },
    { duration, target },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    database_read_duration: ['p(95)<1500', 'p(99)<3000'],
  },
};

export default function () {
  const response = http.get(`${baseUrl}/patients/search?q=&page=1&pageSize=20`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    tags: { workload: 'database-read' },
  });
  queryDuration.add(response.timings.duration);
  check(response, { 'patient read is successful': r => r.status === 200 });
  sleep(1);
}
