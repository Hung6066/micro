// -- Custom Cypress commands for His.Hope E2E tests --

Cypress.Commands.add('login', (username: string, password: string) => {
  cy.session([username, password], () => {
    cy.visit('/login');
    cy.get('[data-cy=username-input]').clear().type(username);
    cy.get('[data-cy=password-input]').clear().type(password);
    cy.get('[data-cy=login-button]').click();
    cy.url().should('not.include', '/login');
    cy.getCookie('access_token').should('exist');
  });
});

Cypress.Commands.add('createPatient', (patientOverrides?: Partial<CreatePatientRequest>) => {
  const defaultPatient: CreatePatientRequest = {
    firstName: 'E2E',
    lastName: 'Patient',
    middleName: '',
    dateOfBirth: '1990-01-15',
    genderCode: 'M',
    phone: '+84123456789',
    email: 'e2e.patient@test.com',
    street: '123 Test Street',
    district: 'District 1',
    city: 'Ho Chi Minh City',
    province: 'HCMC',
    postalCode: '70000',
    country: 'Vietnam',
    insuranceId: 'INS-E2E-001',
    nationalId: '079090001234',
  };

  const patient = { ...defaultPatient, ...patientOverrides };

  cy.request({
    method: 'POST',
    url: `${Cypress.env('apiUrl')}/patients`,
    body: patient,
    headers: {
      Authorization: `Bearer ${Cypress.env('accessToken')}`,
    },
  }).then((response) => {
    expect(response.status).to.eq(201);
    cy.wrap(response.body).as('createdPatient');
  });
});

Cypress.Commands.add('createAppointment', (appointmentOverrides?: Partial<ScheduleAppointmentRequest>) => {
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  const dateStr = tomorrow.toISOString().split('T')[0];

  const defaultAppointment: ScheduleAppointmentRequest = {
    patientId: '',
    providerId: '',
    scheduledDate: dateStr,
    startTime: '09:00:00',
    durationMinutes: 30,
    typeCode: 'CHECKUP',
    reason: 'E2E test appointment',
    location: 'Clinic A',
  };

  const appointment = { ...defaultAppointment, ...appointmentOverrides };

  cy.request({
    method: 'POST',
    url: `${Cypress.env('apiUrl')}/appointments`,
    body: appointment,
    headers: {
      Authorization: `Bearer ${Cypress.env('accessToken')}`,
    },
  }).then((response) => {
    expect(response.status).to.eq(201);
    cy.wrap(response.body).as('createdAppointment');
  });
});

Cypress.Commands.add('startEncounter', (encounterOverrides?: Partial<StartEncounterRequest>) => {
  const defaultEncounter: StartEncounterRequest = {
    patientId: '',
    providerId: '',
    appointmentId: undefined,
    encounterTypeCode: 'OP',
  };

  const encounter = { ...defaultEncounter, ...encounterOverrides };

  cy.request({
    method: 'POST',
    url: `${Cypress.env('apiUrl')}/encounters`,
    body: encounter,
    headers: {
      Authorization: `Bearer ${Cypress.env('accessToken')}`,
    },
  }).then((response) => {
    expect(response.status).to.eq(201);
    cy.wrap(response.body).as('createdEncounter');
  });
});

Cypress.Commands.add('getAccessToken', () => {
  cy.request({
    method: 'POST',
    url: `${Cypress.env('apiUrl')}/auth/login`,
    body: {
      username: Cypress.env('testUsername'),
      password: Cypress.env('testPassword'),
    },
  }).then((response) => {
    Cypress.env('accessToken', response.body.accessToken);
  });
});

// Type definitions for custom commands
interface CreatePatientRequest {
  firstName: string;
  lastName: string;
  middleName?: string;
  dateOfBirth: string;
  genderCode: string;
  phone: string;
  email?: string;
  street: string;
  district: string;
  city: string;
  province: string;
  postalCode?: string;
  country: string;
  insuranceId?: string;
  nationalId?: string;
}

interface ScheduleAppointmentRequest {
  patientId: string;
  providerId: string;
  scheduledDate: string;
  startTime: string;
  durationMinutes: number;
  typeCode: string;
  reason?: string;
  location?: string;
}

interface StartEncounterRequest {
  patientId: string;
  providerId: string;
  appointmentId?: string;
  encounterTypeCode: string;
}

declare global {
  namespace Cypress {
    interface Chainable {
      login(username: string, password: string): Chainable<void>;
      createPatient(overrides?: Partial<CreatePatientRequest>): Chainable<void>;
      createAppointment(overrides?: Partial<ScheduleAppointmentRequest>): Chainable<void>;
      startEncounter(overrides?: Partial<StartEncounterRequest>): Chainable<void>;
      getAccessToken(): Chainable<void>;
    }
  }
}

export {};
