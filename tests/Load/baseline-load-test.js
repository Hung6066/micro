import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const errorRate = new Rate('errors');
const patientLatency = new Trend('patient_latency');
const appointmentLatency = new Trend('appointment_latency');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const AUTH_TOKEN = __ENV.AUTH_TOKEN || 'test-token';

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

const headers = {
    'Authorization': `Bearer ${AUTH_TOKEN}`,
    'Content-Type': 'application/json',
};

export default function () {
    group('Patient Service', () => {
        let res = http.get(`${BASE_URL}/api/v1/patients?page=1&pageSize=20`, { headers });
        check(res, { 'GET /patients status 200': (r) => r.status === 200 });
        errorRate.add(res.status !== 200);
        patientLatency.add(res.timings.duration);
    });

    group('Appointment Service', () => {
        let res = http.get(`${BASE_URL}/api/v1/appointments?page=1&pageSize=20`, { headers });
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
