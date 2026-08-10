/// <reference types="cypress" />

describe('Clinical Encounter Flow', () => {
  before(() => {
    cy.getAccessToken();
  });

  beforeEach(() => {
    cy.login(Cypress.env('testUsername'), Cypress.env('testPassword'));
    cy.visit('/encounters');
    cy.url().should('include', '/encounters');
  });

  it('should display the encounter list page', () => {
    cy.get('[data-cy=encounter-list]').should('be.visible');
    cy.get('[data-cy=encounter-search-input]').should('be.visible');
    cy.get('[data-cy=start-encounter-button]').should('be.visible');
  });

  it('should list encounters with pagination', () => {
    cy.intercept('GET', '**/api/v1/encounters/search*').as('listEncounters');

    cy.wait('@listEncounters').its('response.statusCode').should('eq', 200);

    cy.get('[data-cy=encounter-table]').should('be.visible');
    cy.get('[data-cy=encounter-row]').should('exist');
    cy.get('[data-cy=pagination-controls]').should('be.visible');
  });

  it('should search encounters by keyword', () => {
    cy.intercept('GET', '**/api/v1/encounters/search*').as('searchEncounters');

    cy.get('[data-cy=encounter-search-input]').type('chest pain');
    cy.get('[data-cy=search-submit-button]').click();

    cy.wait('@searchEncounters').its('response.statusCode').should('eq', 200);

    // Results should update
    cy.get('[data-cy=encounter-table]').should('be.visible');
  });

  it('should start a new encounter', () => {
    // First create a patient to use
    cy.createPatient().then(() => {
      cy.get<string>('@createdPatient').then((patient: any) => {
        cy.visit('/encounters/new');
        cy.url().should('include', '/encounters/new');
        cy.get('[data-cy=encounter-form]').should('be.visible');

        cy.intercept('POST', '**/api/v1/encounters').as('startEncounter');

        // Fill the form
        cy.get('[data-cy=patient-search-input]').type(patient.fullName);
        cy.get('[data-cy=patient-search-results]').should('be.visible');
        cy.get('[data-cy=patient-option]').first().click();

        cy.get('[data-cy=encounter-type-select]').select('OP');
        cy.get('[data-cy=chief-complaint-input]').type('Routine checkup');
        cy.get('[data-cy=submit-encounter-button]').click();

        cy.wait('@startEncounter').its('response.statusCode').should('eq', 201);

        // Should navigate to encounter detail
        cy.url().should('match', /\/encounters\/[0-9a-f-]+$/);
        cy.get('[data-cy=encounter-detail]').should('be.visible');
        cy.get('[data-cy=encounter-status]').should('contain', /in progress/i);
      });
    });
  });

  it('should record vitals for an encounter', () => {
    cy.createPatient().then(() => {
      cy.get<string>('@createdPatient').then((patient: any) => {
        cy.startEncounter({ patientId: patient.id, providerId: Cypress.env('testProviderId') || '00000000-0000-0000-0000-000000000001' }).then(() => {
          cy.get<string>('@createdEncounter').then((encounter: any) => {
            cy.visit(`/encounters/${encounter.id}`);

            cy.intercept('POST', `**/api/v1/encounters/${encounter.id}/vitals`).as('recordVitals');

            cy.get('[data-cy=record-vitals-button]').click();
            cy.get('[data-cy=vitals-form]').should('be.visible');

            cy.get('[data-cy=temperature-input]').type('37.0');
            cy.get('[data-cy=heart-rate-input]').type('72');
            cy.get('[data-cy=respiratory-rate-input]').type('16');
            cy.get('[data-cy=systolic-bp-input]').type('120');
            cy.get('[data-cy=diastolic-bp-input]').type('80');
            cy.get('[data-cy=oxygen-saturation-input]').type('98');
            cy.get('[data-cy=weight-input]').type('70');
            cy.get('[data-cy=height-input]').type('175');
            cy.get('[data-cy=save-vitals-button]').click();

            cy.wait('@recordVitals').its('response.statusCode').should('eq', 200);
            cy.get('[data-cy=success-toast]').should('be.visible');
            cy.get('[data-cy=vitals-display]').should('be.visible');
          });
        });
      });
    });
  });

  it('should add a diagnosis to an encounter', () => {
    cy.createPatient().then(() => {
      cy.get<string>('@createdPatient').then((patient: any) => {
        cy.startEncounter({ patientId: patient.id, providerId: '00000000-0000-0000-0000-000000000001' }).then(() => {
          cy.get<string>('@createdEncounter').then((encounter: any) => {
            cy.visit(`/encounters/${encounter.id}`);

            cy.intercept('POST', `**/api/v1/encounters/${encounter.id}/diagnosis`).as('addDiagnosis');

            cy.get('[data-cy=add-diagnosis-button]').click();
            cy.get('[data-cy=diagnosis-form]').should('be.visible');

            cy.get('[data-cy=condition-name-input]').type('Essential Hypertension');
            cy.get('[data-cy=icd10-code-input]').type('I10');
            cy.get('[data-cy=is-primary-checkbox]').check();
            cy.get('[data-cy=diagnosis-notes-input]').type('Confirmed elevated BP readings');
            cy.get('[data-cy=save-diagnosis-button]').click();

            cy.wait('@addDiagnosis').its('response.statusCode').should('eq', 200);
            cy.get('[data-cy=diagnosis-list]').should('contain', 'Essential Hypertension');
          });
        });
      });
    });
  });

  it('should complete an encounter', () => {
    cy.createPatient().then(() => {
      cy.get<string>('@createdPatient').then((patient: any) => {
        cy.startEncounter({ patientId: patient.id, providerId: '00000000-0000-0000-0000-000000000001' }).then(() => {
          cy.get<string>('@createdEncounter').then((encounter: any) => {
            cy.visit(`/encounters/${encounter.id}`);

            cy.intercept('PUT', `**/api/v1/encounters/${encounter.id}/complete`).as('completeEncounter');

            cy.get('[data-cy=complete-encounter-button]').click();
            cy.get('[data-cy=confirm-dialog]').should('be.visible');
            cy.get('[data-cy=confirm-yes-button]').click();

            cy.wait('@completeEncounter').its('response.statusCode').should('eq', 204);

            cy.get('[data-cy=encounter-status]').should('contain', /completed/i);
          });
        });
      });
    });
  });

  it('should navigate between encounter list and detail', () => {
    cy.intercept('GET', '**/api/v1/encounters/search*').as('listEncounters');
    cy.wait('@listEncounters');

    // Click on the first encounter row to view details
    cy.get('[data-cy=encounter-row]').first().click();
    cy.url().should('match', /\/encounters\/[0-9a-f-]+$/);
    cy.get('[data-cy=encounter-detail]').should('be.visible');

    // Navigate back
    cy.get('[data-cy=back-to-list-button]').click();
    cy.url().should('include', '/encounters');
    cy.get('[data-cy=encounter-list]').should('be.visible');
  });
});
