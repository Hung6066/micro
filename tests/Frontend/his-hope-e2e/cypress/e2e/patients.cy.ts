/// <reference types="cypress" />

describe('Patient CRUD Flow', () => {
  before(() => {
    cy.getAccessToken();
  });

  beforeEach(() => {
    cy.login(Cypress.env('testUsername'), Cypress.env('testPassword'));
    cy.visit('/patients');
    cy.url().should('include', '/patients');
  });

  it('should display the patient list page', () => {
    cy.get('[data-cy=patient-list]').should('be.visible');
    cy.get('[data-cy=patient-search-input]').should('be.visible');
    cy.get('[data-cy=add-patient-button]').should('be.visible');
  });

  it('should search for patients by name', () => {
    cy.intercept('GET', '**/api/v1/patients/search*').as('searchPatients');

    cy.get('[data-cy=patient-search-input]').type('Smith');
    cy.get('[data-cy=search-submit-button]').click();

    cy.wait('@searchPatients').its('response.statusCode').should('eq', 200);

    // Results should be displayed
    cy.get('[data-cy=patient-table]').should('be.visible');
    cy.get('[data-cy=patient-row]').should('have.length.at.least', 1);
  });

  it('should show empty state when no patients found', () => {
    cy.intercept('GET', '**/api/v1/patients/search*', {
      body: { items: [], totalCount: 0, page: 1, pageSize: 20 },
    }).as('emptySearch');

    cy.get('[data-cy=patient-search-input]').clear().type('ZZZZNONEXISTENT');
    cy.get('[data-cy=search-submit-button]').click();

    cy.wait('@emptySearch');
    cy.get('[data-cy=no-results-message]').should('be.visible');
  });

  it('should navigate to create patient form', () => {
    cy.get('[data-cy=add-patient-button]').click();
    cy.url().should('include', '/patients/new');
    cy.get('[data-cy=patient-form]').should('be.visible');
  });

  it('should create a new patient with valid data', () => {
    cy.intercept('POST', '**/api/v1/patients').as('createPatient');

    cy.visit('/patients/new');

    // Fill in the patient form
    cy.get('[data-cy=first-name-input]').type('John');
    cy.get('[data-cy=last-name-input]').type('Doe');
    cy.get('[data-cy=date-of-birth-input]').type('1985-06-15');
    cy.get('[data-cy=gender-select]').select('M');
    cy.get('[data-cy=phone-input]').type('+84912345678');
    cy.get('[data-cy=email-input]').type('john.doe@example.com');
    cy.get('[data-cy=street-input]').type('456 Main Street');
    cy.get('[data-cy=district-input]').type('District 1');
    cy.get('[data-cy=city-input]').type('Ho Chi Minh City');
    cy.get('[data-cy=province-input]').type('HCMC');
    cy.get('[data-cy=country-input]').type('Vietnam');
    cy.get('[data-cy=submit-patient-button]').click();

    cy.wait('@createPatient').its('response.statusCode').should('eq', 201);

    // Should navigate to patient detail
    cy.url().should('match', /\/patients\/[0-9a-f-]+$/);
    cy.get('[data-cy=patient-detail]').should('be.visible');
    cy.get('[data-cy=patient-name]').should('contain', 'John Doe');
  });

  it('should show validation errors on empty patient form', () => {
    cy.visit('/patients/new');
    cy.get('[data-cy=submit-patient-button]').click();

    // Required field validations
    cy.get('[data-cy=first-name-error]').should('be.visible');
    cy.get('[data-cy=last-name-error]').should('be.visible');
    cy.get('[data-cy=date-of-birth-error]').should('be.visible');
    cy.get('[data-cy=phone-error]').should('be.visible');
  });

  it('should view patient details', () => {
    cy.createPatient().then(() => {
      cy.get<string>('@createdPatient').then((patient: any) => {
        cy.visit(`/patients/${patient.id}`);
        cy.url().should('include', `/patients/${patient.id}`);
        cy.get('[data-cy=patient-detail]').should('be.visible');
        cy.get('[data-cy=patient-name]').should('contain', patient.fullName);
        cy.get('[data-cy=patient-info-section]').should('be.visible');
        cy.get('[data-cy=patient-allergies-section]').should('be.visible');
        cy.get('[data-cy=patient-conditions-section]').should('be.visible');
      });
    });
  });

  it('should update an existing patient', () => {
    cy.createPatient().then(() => {
      cy.get<string>('@createdPatient').then((patient: any) => {
        cy.visit(`/patients/${patient.id}/edit`);
        cy.url().should('include', '/edit');
        cy.get('[data-cy=patient-form]').should('be.visible');

        cy.intercept('PUT', `**/api/v1/patients/${patient.id}`).as('updatePatient');

        // Update the phone number
        cy.get('[data-cy=phone-input]').clear().type('+84987654321');
        cy.get('[data-cy=submit-patient-button]').click();

        cy.wait('@updatePatient').its('response.statusCode').should('eq', 200);

        // Should redirect to detail page
        cy.url().should('include', `/patients/${patient.id}`);
        cy.get('[data-cy=success-toast]').should('be.visible');
      });
    });
  });

  it('should deactivate and reactivate a patient', () => {
    cy.createPatient().then(() => {
      cy.get<string>('@createdPatient').then((patient: any) => {
        cy.visit(`/patients/${patient.id}`);

        // Deactivate
        cy.intercept('PATCH', `**/api/v1/patients/${patient.id}/deactivate`).as('deactivatePatient');
        cy.get('[data-cy=deactivate-patient-button]').click();
        cy.get('[data-cy=confirm-dialog]').should('be.visible');
        cy.get('[data-cy=confirm-yes-button]').click();
        cy.wait('@deactivatePatient').its('response.statusCode').should('eq', 200);

        cy.get('[data-cy=patient-status]').should('contain', /inactive|deactivated/i);

        // Reactivate
        cy.intercept('PATCH', `**/api/v1/patients/${patient.id}/reactivate`).as('reactivatePatient');
        cy.get('[data-cy=reactivate-patient-button]').click();
        cy.get('[data-cy=confirm-yes-button]').click();
        cy.wait('@reactivatePatient').its('response.statusCode').should('eq', 200);

        cy.get('[data-cy=patient-status]').should('contain', /active/i);
      });
    });
  });
});
