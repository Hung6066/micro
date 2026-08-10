import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Counter, Trend } from 'k6/metrics';

/**
 * Spike Test for His.Hope EMR System
 *
 * Simulates sudden traffic surges to test system resilience:
 * - Rapid user onboarding spike
 * - Patient data burst creation
 * - Concurrent encounter creation
 *
 * This test verifies that the system can handle sudden load spikes
 * and recover gracefully without breaking.
 */

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5011/api/v1';
const AUTH_TOKEN = __ENV.AUTH_TOKEN || '';

export const options = {
  stages: [
    { duration: '30s', target: 5 },    // Normal load
    { duration: '10s', target: 100 },  // Spike to 100 users
    { duration: '30s', target: 100 },  // Stay at spike
    { duration: '10s', target: 5 },    // Drop back down
    { duration: '1m', target: 5 },     // Recovery period
    { duration: '10s', target: 200 },  // Second spike to 200 users
    { duration: '30s', target: 200 },  // Stay at second spike
    { duration: '30s', target: 0 },    // Ramp down to zero
  ],
  thresholds: {
    http_req_duration: ['p(90)<5000', 'p(95)<10000'],
    http_req_failed: ['rate<0.05'],
    http_reqs: ['rate>50'], // Must sustain at least 50 req/s
  },
};

const spikeErrors = new Counter('spike_errors');
const requestLatency = new Trend('request_latency', true);
const spikeRecoveryTime = new Trend('spike_recovery_time', true);

function getAuthHeaders() {
  const headers = { 'Content-Type': 'application/json' };
  if (AUTH_TOKEN) {
    headers['Authorization'] = `Bearer ${AUTH_TOKEN}`;
  }
  return headers;
}

function measureLatency(name, fn) {
  const start = Date.now();
  try {
    return fn();
  } finally {
    const elapsed = Date.now() - start;
    requestLatency.add(elapsed);
  }
}

export default function () {
  const vuId = __VU;
  const iterId = __ITER;

  // Mix of API operations to simulate realistic spike behavior
  const operation = Math.floor(Math.random() * 10);

  group('Spike Test Operations', () => {
    let res;

    switch (true) {
      case operation < 4: {
        // 40% - Patient search (lightweight)
        res = measureLatency('search', () =>
          http.get(`${BASE_URL}/patients/search?q=Smith&page=1&pageSize=20`, {
            headers: getAuthHeaders(),
          })
        );
        check(res, {
          'search OK': (r) => r.status === 200,
        });
        break;
      }

      case operation < 6: {
        // 20% - Encounter search
        res = measureLatency('encounter-list', () =>
          http.get(`${BASE_URL}/encounters/search?page=1&pageSize=20`, {
            headers: getAuthHeaders(),
          })
        );
        check(res, {
          'encounter list OK': (r) => r.status === 200,
        });
        break;
      }

      case operation < 8: {
        // 20% - Dashboard stats
        res = measureLatency('dashboard', () =>
          http.get(`${BASE_URL}/dashboard/stats`, {
            headers: getAuthHeaders(),
          })
        );
        check(res, {
          'dashboard OK': (r) => r.status === 200,
        });
        break;
      }

      case operation < 9: {
        // 10% - Create patient (heavy write)
        const patientPayload = JSON.stringify({
          firstName: `Spike`,
          lastName: `Test_${vuId}_${iterId}`,
          dateOfBirth: '1990-01-15',
          genderCode: vuId % 2 === 0 ? 'M' : 'F',
          phone: `+8491${String(vuId).padStart(4, '0')}${String(iterId).padStart(4, '0')}`,
          email: `spike.${vuId}.${iterId}@test.com`,
          street: `${iterId} Spike Street`,
          district: 'District 1',
          city: 'Ho Chi Minh City',
          province: 'HCMC',
          country: 'Vietnam',
        });

        res = measureLatency('create-patient', () =>
          http.post(`${BASE_URL}/patients`, patientPayload, {
            headers: getAuthHeaders(),
          })
        );
        check(res, {
          'patient created': (r) => r.status === 201,
        });
        break;
      }

      default: {
        // 10% - Start encounter (heavy write)
        const encounterPayload = JSON.stringify({
          patientId: '00000000-0000-0000-0000-000000000001',
          providerId: '00000000-0000-0000-0000-000000000002',
          encounterTypeCode: 'ER',
          chiefComplaint: 'Spike test emergency encounter',
        });

        res = measureLatency('start-encounter', () =>
          http.post(`${BASE_URL}/encounters`, encounterPayload, {
            headers: getAuthHeaders(),
          })
        );
        check(res, {
          'encounter started': (r) => r.status === 201,
        });
        break;
      }
    }

    if (res && res.status >= 400) {
      spikeErrors.add(1);
      console.error(
        `[Spike] VU=${vuId} Iter=${iterId} Op=${operation} Status=${res.status} Duration=${res.timings.duration}ms`
      );
    }

    // Minimal sleep to maximize throughput during spikes
    sleep(Math.random() * 0.5 + 0.1);
  });
}
