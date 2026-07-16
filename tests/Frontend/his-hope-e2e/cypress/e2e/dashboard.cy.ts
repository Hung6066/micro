/// <reference types="cypress" />

describe('Dashboard', () => {
  before(() => {
    cy.getAccessToken();
  });

  beforeEach(() => {
    cy.login(Cypress.env('testUsername'), Cypress.env('testPassword'));
    cy.visit('/dashboard');
    cy.url().should('include', '/dashboard');
  });

  it('should display the dashboard page', () => {
    cy.get('[data-cy=dashboard]').should('be.visible');
    cy.get('[data-cy=dashboard-title]').should('contain', /dashboard|overview/i);
  });

  it('should load and display all stat cards', () => {
    cy.intercept('GET', '**/api/v1/dashboard/stats').as('getStats');

    cy.wait('@getStats').its('response.statusCode').should('eq', 200);

    // Verify all stat cards are displayed
    cy.get('[data-cy=stat-card-total-patients]').should('be.visible');
    cy.get('[data-cy=stat-card-total-patients]').find('[data-cy=stat-value]').should('not.be.empty');

    cy.get('[data-cy=stat-card-today-appointments]').should('be.visible');
    cy.get('[data-cy=stat-card-today-appointments]').find('[data-cy=stat-value]').should('not.be.empty');

    cy.get('[data-cy=stat-card-active-encounters]').should('be.visible');
    cy.get('[data-cy=stat-card-active-encounters]').find('[data-cy=stat-value]').should('not.be.empty');

    cy.get('[data-cy=stat-card-pending-diagnoses]').should('be.visible');
    cy.get('[data-cy=stat-card-pending-diagnoses]').find('[data-cy=stat-value]').should('not.be.empty');
  });

  it('should display correct stat values from the API', () => {
    const mockStats = {
      totalPatients: 1523,
      todayAppointments: 47,
      activeEncounters: 89,
      pendingDiagnoses: 34,
    };

    cy.intercept('GET', '**/api/v1/dashboard/stats', {
      body: mockStats,
    }).as('getMockStats');

    cy.visit('/dashboard'); // reload to use intercept
    cy.wait('@getMockStats');

    cy.get('[data-cy=stat-card-total-patients] [data-cy=stat-value]').should('contain', '1,523');
    cy.get('[data-cy=stat-card-today-appointments] [data-cy=stat-value]').should('contain', '47');
    cy.get('[data-cy=stat-card-active-encounters] [data-cy=stat-value]').should('contain', '89');
    cy.get('[data-cy=stat-card-pending-diagnoses] [data-cy=stat-value]').should('contain', '34');
  });

  it('should handle zero values gracefully', () => {
    const emptyStats = {
      totalPatients: 0,
      todayAppointments: 0,
      activeEncounters: 0,
      pendingDiagnoses: 0,
    };

    cy.intercept('GET', '**/api/v1/dashboard/stats', {
      body: emptyStats,
    }).as('getEmptyStats');

    cy.visit('/dashboard');
    cy.wait('@getEmptyStats');

    cy.get('[data-cy=stat-card-total-patients] [data-cy=stat-value]').should('contain', '0');
    cy.get('[data-cy=stat-card-today-appointments] [data-cy=stat-value]').should('contain', '0');
    cy.get('[data-cy=stat-card-active-encounters] [data-cy=stat-value]').should('contain', '0');
    cy.get('[data-cy=stat-card-pending-diagnoses] [data-cy=stat-value]').should('contain', '0');
  });

  it('should display recent activity section', () => {
    cy.intercept('GET', '**/api/v1/dashboard/stats').as('getStats');
    cy.wait('@getStats');

    cy.get('[data-cy=recent-activity]').should('be.visible');
    cy.get('[data-cy=recent-appointments]').should('be.visible');
    cy.get('[data-cy=recent-encounters]').should('be.visible');
  });

  it('should navigate to other sections from dashboard', () => {
    cy.intercept('GET', '**/api/v1/dashboard/stats').as('getStats');
    cy.wait('@getStats');

    // Navigate to patients via dashboard link
    cy.get('[data-cy=nav-patients]').click();
    cy.url().should('include', '/patients');

    // Go back to dashboard
    cy.visit('/dashboard');
    cy.wait('@getStats');

    // Navigate to appointments via dashboard link
    cy.get('[data-cy=nav-appointments]').click();
    cy.url().should('include', '/appointments');

    // Go back to dashboard
    cy.visit('/dashboard');
    cy.wait('@getStats');

    // Navigate to encounters via dashboard card
    cy.get('[data-cy=stat-card-active-encounters]').click();
    cy.url().should('include', '/encounters');
  });

  it('should refresh stats when refresh button is clicked', () => {
    cy.intercept('GET', '**/api/v1/dashboard/stats').as('getStats');
    cy.wait('@getStats');

    cy.intercept('GET', '**/api/v1/dashboard/stats').as('refreshedStats');
    cy.get('[data-cy=refresh-stats-button]').click();

    cy.wait('@refreshedStats').its('response.statusCode').should('eq', 200);
  });

  it('should show error state when API fails', () => {
    cy.intercept('GET', '**/api/v1/dashboard/stats', {
      statusCode: 500,
      body: { error: 'Internal Server Error' },
    }).as('failedStats');

    cy.visit('/dashboard');
    cy.wait('@failedStats');

    cy.get('[data-cy=dashboard-error]')
      .should('be.visible')
      .and('contain', /failed|error|unable/i);

    // Retry button should be available
    cy.get('[data-cy=retry-stats-button]').should('be.visible');
  });
});
