import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const errorRate = new Rate('errors');
const patientLatency = new Trend('patient_latency');
const appointmentLatency = new Trend('appointment_latency');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const AUTH_TOKEN = __ENV.AUTH_TOKEN || '';

export const options = {
    stages: [
        { duration: '2m', target: 50 },
        { duration: '5m', target: 50 },
        { duration: '2m', target: 100 },
        { duration: '5m', target: 100 },
        { duration: '2m', target: 200 },
        { duration: '3m', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<500'],
        errors: ['rate<0.01'],
    },
};

function requestHeaders() {
    // Local Docker runs collapse all VUs onto one source IP. Rotate a
    // synthetic forwarded client address per iteration so the test exercises
    // service capacity instead of tripping the production per-IP abuse limit.
    // Ingress must overwrite this header in real deployments.
    const clientIp = `10.200.${__VU % 250}.${(__ITER % 250) + 1}`;
    return {
        'Authorization': `Bearer ${AUTH_TOKEN}`,
        'Content-Type': 'application/json',
        'X-Forwarded-For': clientIp,
    };
}

export function setup() {
    if (!AUTH_TOKEN) {
        throw new Error('AUTH_TOKEN is required for the authenticated enterprise load baseline; refusing to run with a placeholder token.');
    }
}

export default function () {
    group('Patient Service', () => {
        let res = http.get(`${BASE_URL}/api/v1/patients?page=1&pageSize=20`, { headers: requestHeaders() });
        check(res, { 'GET /patients status 200': (r) => r.status === 200 });
        errorRate.add(res.status !== 200);
        patientLatency.add(res.timings.duration);
    });

    group('Appointment Service', () => {
        let res = http.get(`${BASE_URL}/api/v1/appointments?page=1&pageSize=20`, { headers: requestHeaders() });
        check(res, { 'GET /appointments status 200': (r) => r.status === 200 });
        errorRate.add(res.status !== 200);
        appointmentLatency.add(res.timings.duration);
    });

    group('Health Check', () => {
        let res = http.get(`${BASE_URL}/health`);
        check(res, { 'Health check OK': (r) => r.status === 200 });
    });

    sleep(0.5);
}

export function handleSummary(data) {
    return {
        'tests/load/results/baseline-summary.json': JSON.stringify(data, null, 2),
    };
}
