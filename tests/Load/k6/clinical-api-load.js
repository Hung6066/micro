import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

/**
 * Clinical API Load Test
 * 
 * Simulates clinical workflow load:
 * - List/search encounters
 * - Start new encounters
 * - Record vitals
 * - Add diagnoses
 * - Complete encounters
 * 
 * Targets: API Gateway at localhost:5011
 */

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5011/api/v1';
const AUTH_TOKEN = __ENV.AUTH_TOKEN || '';

export const options = {
  stages: [
    { duration: '1m', target: 5 },    // Ramp up to 5 users
    { duration: '2m', target: 15 },   // Ramp up to 15 users
    { duration: '3m', target: 30 },   // Ramp up to 30 users
    { duration: '2m', target: 30 },   // Stay at 30 users
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<3000', 'p(99)<8000'],
    http_req_failed: ['rate<0.02'],
    encounter_start_duration: ['p(95)<2500'],
    encounter_list_duration: ['p(95)<2000'],
  },
};

// Custom metrics
const encounterListDuration = new Trend('encounter_list_duration', true);
const encounterViewDuration = new Trend('encounter_view_duration', true);
const encounterStartDuration = new Trend('encounter_start_duration', true);
const encounterVitalsDuration = new Trend('encounter_vitals_duration', true);
const encounterDiagnosisDuration = new Trend('encounter_diagnosis_duration', true);
const encounterCompleteDuration = new Trend('encounter_complete_duration', true);
const encounterErrors = new Counter('encounter_errors');

const ENCOUNTER_TYPES = ['OP', 'IP', 'ER', 'TH', 'FU', 'AW'];
const CHIEF_COMPLAINTS = [
  'Chest pain',
  'Headache',
  'Abdominal pain',
  'Shortness of breath',
  'Fever and cough',
  'Back pain',
  'Routine checkup',
  'Follow-up visit',
];
const DIAGNOSES = [
  { condition: 'Essential Hypertension', icd10: 'I10' },
  { condition: 'Type 2 Diabetes', icd10: 'E11' },
  { condition: 'Acute Upper Respiratory Infection', icd10: 'J06' },
  { condition: 'Urinary Tract Infection', icd10: 'N39' },
  { condition: 'Acute Bronchitis', icd10: 'J20' },
];

function getAuthHeaders() {
  const headers = { 'Content-Type': 'application/json' };
  if (AUTH_TOKEN) {
    headers['Authorization'] = `Bearer ${AUTH_TOKEN}`;
  }
  return headers;
}

export default function () {
  // Phase 1: List encounters (most common operation)
  group('Encounter List', () => {
    const listUrl = `${BASE_URL}/encounters/search?page=1&pageSize=20`;
    const listRes = http.get(listUrl, { headers: getAuthHeaders() });
    encounterListDuration.add(listRes.timings.duration);

    check(listRes, {
      'list returned 200': (r) => r.status === 200,
    });

    if (listRes.status !== 200) {
      encounterErrors.add(1);
    }

    sleep(Math.random() * 1 + 0.5);
  });

  // Phase 2: View encounter details
  group('Encounter View', () => {
    const searchRes = http.get(
      `${BASE_URL}/encounters/search?page=1&pageSize=5`,
      { headers: getAuthHeaders() }
    );

    if (searchRes.status === 200) {
      try {
        const body = JSON.parse(searchRes.body);
        const items = body.items || body.data || [];
        if (items.length > 0) {
          const encounterId = items[0].id;
          const viewRes = http.get(`${BASE_URL}/encounters/${encounterId}`, {
            headers: getAuthHeaders(),
          });
          encounterViewDuration.add(viewRes.timings.duration);

          check(viewRes, {
            'view returned 200': (r) => r.status === 200,
          });
        }
      } catch (e) {
        console.error('Failed to parse encounter view response:', e);
      }
    }

    sleep(Math.random() * 1 + 0.5);
  });

  // Phase 3: Search encounters
  group('Encounter Search', () => {
    const searchTerms = ['chest', 'fever', 'pain', 'follow', 'routine'];
    const term = searchTerms[Math.floor(Math.random() * searchTerms.length)];

    const searchRes = http.get(
      `${BASE_URL}/encounters/search?q=${term}&page=1&pageSize=20`,
      { headers: getAuthHeaders() }
    );

    check(searchRes, {
      'search returned 200': (r) => r.status === 200,
    });

    sleep(Math.random() * 1 + 0.3);
  });

  // Phase 4: Start new encounter (less frequent, heavier operation)
  group('Encounter Start', () => {
    const patientId = '00000000-0000-0000-0000-000000000001'; // placeholder
    const providerId = '00000000-0000-0000-0000-000000000002'; // placeholder
    const encounterType = ENCOUNTER_TYPES[Math.floor(Math.random() * ENCOUNTER_TYPES.length)];
    const chiefComplaint = CHIEF_COMPLAINTS[Math.floor(Math.random() * CHIEF_COMPLAINTS.length)];

    const startPayload = JSON.stringify({
      patientId,
      providerId,
      encounterTypeCode: encounterType,
      chiefComplaint,
    });

    const startRes = http.post(`${BASE_URL}/encounters`, startPayload, {
      headers: getAuthHeaders(),
    });
    encounterStartDuration.add(startRes.timings.duration);

    check(startRes, {
      'start returned 201': (r) => r.status === 201,
      'start returned encounter with id': (r) => {
        try {
          const body = JSON.parse(r.body);
          return body && body.id !== undefined;
        } catch {
          return false;
        }
      },
    });

    if (startRes.status === 201) {
      try {
        const encounter = JSON.parse(startRes.body);
        const encounterId = encounter.id;

        // Phase 4a: Record vitals
        sleep(Math.random() * 1 + 0.5);

        group('Record Vitals', () => {
          const vitalsPayload = JSON.stringify({
            temperature: 37.0,
            heartRate: 72,
            respiratoryRate: 16,
            systolicBP: 120,
            diastolicBP: 80,
            oxygenSaturation: 98.0,
            heightCm: 170.0,
            weightKg: 70.0,
            bmi: 24.2,
          });

          const vitalsRes = http.post(
            `${BASE_URL}/encounters/${encounterId}/vitals`,
            vitalsPayload,
            { headers: getAuthHeaders() }
          );
          encounterVitalsDuration.add(vitalsRes.timings.duration);

          check(vitalsRes, {
            'vitals returned 200': (r) => r.status === 200,
          });
        });

        // Phase 4b: Add diagnosis
        sleep(Math.random() * 0.5 + 0.3);

        group('Add Diagnosis', () => {
          const diagnosis = DIAGNOSES[Math.floor(Math.random() * DIAGNOSES.length)];
          const diagnosisPayload = JSON.stringify({
            conditionName: diagnosis.condition,
            icd10Code: diagnosis.icd10,
            isPrimary: true,
            notes: 'Diagnosed during load test',
          });

          const diagnosisRes = http.post(
            `${BASE_URL}/encounters/${encounterId}/diagnosis`,
            diagnosisPayload,
            { headers: getAuthHeaders() }
          );
          encounterDiagnosisDuration.add(diagnosisRes.timings.duration);

          check(diagnosisRes, {
            'diagnosis returned 200': (r) => r.status === 200,
          });
        });

        // Phase 4c: Complete encounter (about 30% chance)
        if (Math.random() < 0.3) {
          group('Complete Encounter', () => {
            const completeRes = http.put(
              `${BASE_URL}/encounters/${encounterId}/complete`,
              {},
              { headers: getAuthHeaders() }
            );
            encounterCompleteDuration.add(completeRes.timings.duration);

            check(completeRes, {
              'complete returned 204': (r) => r.status === 204,
            });
          });
        }
      } catch (e) {
        console.error('Failed during encounter workflow:', e);
      }
    } else {
      encounterErrors.add(1);
    }

    sleep(Math.random() * 2 + 1);
  });
}
