// @ts-nocheck
import { Patient } from '@core/models/patient.model';
import { Encounter } from '@core/models/encounter.model';
import { Appointment } from '@core/models/appointment.model';
import { Medication } from '@core/models/medication.model';
import { Prescription } from '@core/models/prescription.model';
import { LabOrder } from '@core/models/lab-order.model';
import { Invoice } from '@core/models/invoice.model';
import { User } from '@core/models/auth.model';
import { PagedResult } from '@core/models/paged-result.model';

let _sequenceId = 0;
function seq(prefix: string): string {
  _sequenceId++;
  return `${prefix}-${String(_sequenceId).padStart(3, '0')}`;
}

function pastDays(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString();
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

export function createMockPatient(overrides?: Partial<Patient>): Patient {
  const id = overrides?.id ?? seq('pat');
  return {
    id,
    fullName: `Patient ${id}`,
    firstName: 'First',
    lastName: 'Last',
    middleName: 'M',
    dateOfBirth: '1990-01-15',
    age: 34,
    genderCode: 'M',
    genderName: 'Male',
    phone: '0900000001',
    email: `patient.${id}@email.com`,
    street: '123 Main St',
    district: 'District 1',
    city: 'Ho Chi Minh City',
    province: 'Ho Chi Minh',
    postalCode: '70000',
    country: 'Viet Nam',
    bloodTypeCode: 'A+',
    bloodTypeName: 'A Rh(D) Positive',
    maritalStatusCode: 'single',
    insuranceId: 'SV0000000001',
    nationalId: '079000000001',
    occupation: 'Engineer',
    emergencyContactName: 'Emergency Contact',
    emergencyContactPhone: '0909999999',
    isActive: true,
    createdAt: pastDays(30),
    updatedAt: pastDays(1),
    allergies: [],
    conditions: [],
    ...overrides,
  };
}

export function createMockPatientList(count: number): Patient[] {
  return Array.from({ length: count }, (_, i) =>
    createMockPatient({ id: `pat-${String(i + 1).padStart(3, '0')}` }),
  );
}

export function createMockAppointment(overrides?: Partial<Appointment>): Appointment {
  const id = overrides?.id ?? seq('apt');
  return {
    id,
    patientId: 'pat-001',
    providerId: 'usr-001',
    scheduledDate: today(),
    startTime: '08:00',
    endTime: '08:30',
    status: 'scheduled',
    statusName: 'Scheduled',
    type: 'consultation',
    typeName: 'Consultation',
    reason: 'Checkup',
    location: 'Room 101',
    createdAt: pastDays(7),
    updatedAt: pastDays(1),
    ...overrides,
  };
}

export function createMockEncounter(overrides?: Partial<Encounter>): Encounter {
  const id = overrides?.id ?? seq('enc');
  return {
    id,
    patientId: 'pat-001',
    providerId: 'usr-001',
    appointmentId: 'apt-001',
    encounterDate: today(),
    encounterType: 'consultation',
    encounterTypeName: 'Consultation',
    status: 'in_progress',
    statusName: 'In Progress',
    chiefComplaint: 'Chest pain',
    assessment: 'Stable',
    plan: 'Rest and follow up',
    diagnosisNotes: 'Pending tests',
    vitalSigns: {
      temperature: 37.0,
      heartRate: 78,
      respiratoryRate: 16,
      systolicBP: 120,
      diastolicBP: 80,
      oxygenSaturation: 99,
      heightCm: 170,
      weightKg: 70,
      bmi: 24.2,
    },
    hpi: {
      onset: '2 days ago',
      location: 'Chest',
      duration: 'Intermittent',
      characteristics: 'Sharp pain',
      aggravatingFactors: 'Deep breath',
      relievingFactors: 'Rest',
      priorTreatments: 'None',
    },
    diagnoses: [
      { conditionName: 'Hypertension', icd10Code: 'I10', isPrimary: true, notes: 'Mild' },
    ],
    createdAt: pastDays(0),
    updatedAt: pastDays(0),
    ...overrides,
  };
}

export function createMockInvoice(overrides?: Partial<Invoice>): Invoice {
  const id = overrides?.id ?? seq('inv');
  return {
    id,
    patientId: 'pat-001',
    encounterId: 'enc-001',
    invoiceNumber: `INV-${id}`,
    invoiceDate: today(),
    dueDate: pastDays(-30),
    statusCode: 'issued',
    statusName: 'Issued',
    subTotal: 500000,
    taxAmount: 50000,
    discountAmount: 0,
    totalAmount: 550000,
    paidAmount: 0,
    balanceDue: 550000,
    createdAt: pastDays(0),
    lineItems: [
      {
        id: seq('li'),
        description: 'Consultation fee',
        quantity: 1,
        unitPrice: 300000,
        amount: 300000,
        itemCode: 'SVC-001',
        itemTypeCode: 'exam',
        itemTypeName: 'Examination',
      },
    ],
    payments: [],
    patientName: 'Test Patient',
    ...overrides,
  };
}

export function createMockLabOrder(overrides?: Partial<LabOrder>): LabOrder {
  const id = overrides?.id ?? seq('lab');
  return {
    id,
    patientId: 'pat-001',
    providerId: 'usr-001',
    encounterId: 'enc-001',
    orderDate: today(),
    statusCode: 'ordered',
    statusName: 'Ordered',
    priorityCode: 'routine',
    priorityName: 'Routine',
    notes: 'Routine labs',
    tests: [
      {
        id: seq('lt'),
        testCode: 'CBC',
        testName: 'Complete Blood Count',
        specimenType: 'blood',
        statusCode: 'ordered',
        statusName: 'Ordered',
        orderedAt: pastDays(0),
      },
    ],
    patientName: 'Test Patient',
    providerName: 'Test Provider',
    ...overrides,
  };
}

export function createMockMedication(overrides?: Partial<Medication>): Medication {
  const id = overrides?.id ?? seq('med');
  return {
    id,
    name: `Medication ${id}`,
    genericName: 'Generic Name',
    brandName: 'Brand Name',
    dosageForm: 'tablet',
    strength: '500mg',
    route: 'oral',
    requiresPrescription: true,
    isActive: true,
    createdAt: pastDays(365),
    ...overrides,
  };
}

export function createMockPrescription(overrides?: Partial<Prescription>): Prescription {
  const id = overrides?.id ?? seq('rx');
  return {
    id,
    patientId: 'pat-001',
    providerId: 'usr-001',
    medicationId: 'med-001',
    medicationName: 'Amoxicillin 500mg',
    strength: '500mg',
    dosageForm: 'tablet',
    dosageInstructions: 'Take 1 tablet 3 times daily',
    route: 'oral',
    quantity: 30,
    refills: 2,
    statusCode: 'active',
    statusName: 'Active',
    prescribedAt: pastDays(0),
    createdAt: pastDays(0),
    patientName: 'Test Patient',
    providerName: 'Test Provider',
    ...overrides,
  };
}

export function createMockUser(overrides?: Partial<User>): User {
  const id = overrides?.id ?? seq('usr');
  return {
    id,
    username: `user.${id}`,
    email: `user.${id}@hishope.vn`,
    firstName: 'Test',
    lastName: 'User',
    fullName: `Test User (${id})`,
    roles: ['doctor'],
    ...overrides,
  };
}

export function createMockPagedResult<T>(
  items: T[],
  totalCount?: number,
): PagedResult<T> {
  return {
    items,
    totalCount: totalCount ?? items.length,
    page: 1,
    pageSize: items.length || 10,
    hasNextPage: false,
    hasPreviousPage: false,
  };
}
