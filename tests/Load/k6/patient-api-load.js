import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

/**
 * Patient API Load Test
 * 
 * Simulates realistic patient management workload:
 * - Search patients (most frequent)
 * - View patient details
 * - Create new patients
 * - Update existing patients
 * 
 * Targets: API Gateway at localhost:5011
 */

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5011/api/v1';
const AUTH_TOKEN = __ENV.AUTH_TOKEN || '';

export const options = {
  stages: [
    { duration: '1m', target: 10 },   // Ramp up to 10 users
    { duration: '2m', target: 25 },   // Ramp up to 25 users
    { duration: '3m', target: 50 },   // Ramp up to 50 users
    { duration: '2m', target: 50 },   // Stay at 50 users
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000', 'p(99)<5000'],
    http_req_failed: ['rate<0.01'],
    patient_search_duration: ['p(95)<1500'],
    patient_create_duration: ['p(95)<3000'],
  },
};

// Custom metrics
const patientSearchDuration = new Trend('patient_search_duration', true);
const patientViewDuration = new Trend('patient_view_duration', true);
const patientCreateDuration = new Trend('patient_create_duration', true);
const patientUpdateDuration = new Trend('patient_update_duration', true);
const patientErrors = new Counter('patient_errors');

const COMMON_NAMES = ['Smith', 'Johnson', 'Williams', 'Brown', 'Jones', 'Garcia', 'Miller', 'Davis'];
const GENDERS = ['M', 'F'];

function getAuthHeaders() {
  const headers = {
    'Content-Type': 'application/json',
  };
  if (AUTH_TOKEN) {
    headers['Authorization'] = `Bearer ${AUTH_TOKEN}`;
  }
  return headers;
}

function generatePatient(index) {
  const lastName = COMMON_NAMES[index % COMMON_NAMES.length];
  return JSON.stringify({
    firstName: `LoadTest`,
    lastName: `${lastName}_${__VU}_${index}`,
    dateOfBirth: '1985-06-15',
    genderCode: GENDERS[index % 2],
    phone: `+8491${String(index).padStart(8, '0')}`,
    email: `patient.${__VU}.${index}@loadtest.com`,
    street: `${index + 1} Test Street`,
    district: 'District 1',
    city: 'Ho Chi Minh City',
    province: 'HCMC',
    country: 'Vietnam',
  });
}

export default function () {
  group('Patient Search', () => {
    const searchTerm = COMMON_NAMES[Math.floor(Math.random() * COMMON_NAMES.length)];
    const searchUrl = `${BASE_URL}/patients/search?q=${searchTerm}&page=1&pageSize=20`;

    const searchRes = http.get(searchUrl, { headers: getAuthHeaders() });
    patientSearchDuration.add(searchRes.timings.duration);

    check(searchRes, {
      'search returned 200': (r) => r.status === 200,
      'search response has items or empty': (r) => {
        const body = JSON.parse(r.body);
        return body && Array.isArray(body.items || body.data || body);
      },
    });

    if (searchRes.status !== 200) {
      patientErrors.add(1);
    }

    sleep(Math.random() * 2 + 1);
  });

  group('Patient View', () => {
    // First search to get a patient ID
    const searchRes = http.get(
      `${BASE_URL}/patients/search?q=Smith&page=1&pageSize=5`,
      { headers: getAuthHeaders() }
    );

    if (searchRes.status === 200) {
      try {
        const body = JSON.parse(searchRes.body);
        const items = body.items || body.data || [];
        if (items.length > 0) {
          const patientId = items[0].id;
          const viewRes = http.get(`${BASE_URL}/patients/${patientId}`, {
            headers: getAuthHeaders(),
          });
          patientViewDuration.add(viewRes.timings.duration);

          check(viewRes, {
            'view returned 200': (r) => r.status === 200,
            'view returned patient data': (r) => {
              const data = JSON.parse(r.body);
              return data && data.id === patientId;
            },
          });
        }
      } catch (e) {
        console.error('Failed to parse search response:', e);
      }
    }

    sleep(Math.random() * 1 + 0.5);
  });

  group('Patient Create', () => {
    const uniqueIndex = Math.floor(Math.random() * 100000);
    const patientData = generatePatient(uniqueIndex);

    const createRes = http.post(`${BASE_URL}/patients`, patientData, {
      headers: getAuthHeaders(),
    });
    patientCreateDuration.add(createRes.timings.duration);

    check(createRes, {
      'create returned 201': (r) => r.status === 201,
      'create returned patient with id': (r) => {
        try {
          const body = JSON.parse(r.body);
          return body && body.id !== undefined;
        } catch {
          return false;
        }
      },
    });

    if (createRes.status === 201) {
      // Update the created patient
      try {
        const createdPatient = JSON.parse(createRes.body);
        const patientId = createdPatient.id;

        const updateData = JSON.stringify({
          ...JSON.parse(patientData),
          firstName: 'Updated',
          phone: `+8499${String(uniqueIndex).padStart(8, '0')}`,
        });

        const updateRes = http.put(`${BASE_URL}/patients/${patientId}`, updateData, {
          headers: getAuthHeaders(),
        });
        patientUpdateDuration.add(updateRes.timings.duration);

        check(updateRes, {
          'update returned 200': (r) => r.status === 200,
        });
      } catch (e) {
        console.error('Failed during update:', e);
      }
    } else {
      patientErrors.add(1);
    }

    sleep(Math.random() * 2 + 1);
  });
}
