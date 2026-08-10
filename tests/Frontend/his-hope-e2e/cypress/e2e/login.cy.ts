/// <reference types="cypress" />

describe('Login Flow', () => {
  beforeEach(() => {
    cy.visit('/login');
    // Ensure we are on the login page
    cy.url().should('include', '/login');
  });

  it('should display the login form with all required elements', () => {
    cy.get('[data-cy=login-form]').should('be.visible');
    cy.get('[data-cy=username-input]').should('be.visible');
    cy.get('[data-cy=password-input]').should('be.visible');
    cy.get('[data-cy=login-button]').should('be.visible').and('contain', /sign in|login/i);
    cy.get('[data-cy=register-link]').should('be.visible');
  });

  it('should show validation errors when fields are empty', () => {
    cy.get('[data-cy=login-button]').click();

    // Username validation
    cy.get('[data-cy=username-error]')
      .should('be.visible')
      .and('contain', /required/i);

    // Password validation
    cy.get('[data-cy=password-error]')
      .should('be.visible')
      .and('contain', /required/i);

    // Verify login was not attempted
    cy.url().should('include', '/login');
  });

  it('should show validation error for short password', () => {
    cy.get('[data-cy=username-input]').type('testuser');
    cy.get('[data-cy=password-input]').type('ab');
    cy.get('[data-cy=login-button]').click();

    cy.get('[data-cy=password-error]')
      .should('be.visible')
      .and('contain', /at least|minimum/i);
  });

  it('should successfully login with valid credentials', () => {
    cy.intercept('POST', '**/api/v1/auth/login').as('loginRequest');

    cy.get('[data-cy=username-input]').type(Cypress.env('testUsername'));
    cy.get('[data-cy=password-input]').type(Cypress.env('testPassword'));
    cy.get('[data-cy=login-button]').click();

    cy.wait('@loginRequest').its('response.statusCode').should('eq', 200);

    // After successful login, should redirect away from login page
    cy.url().should('not.include', '/login');

    // Welcome/greeting element should be visible
    cy.get('[data-cy=user-greeting]').should('be.visible');
    cy.get('[data-cy=logout-button]').should('be.visible');
  });

  it('should show error on invalid credentials', () => {
    cy.intercept('POST', '**/api/v1/auth/login').as('loginRequest');

    cy.get('[data-cy=username-input]').type('invalid_user');
    cy.get('[data-cy=password-input]').type('WrongPass123!');
    cy.get('[data-cy=login-button]').click();

    cy.wait('@loginRequest').its('response.statusCode').should('eq', 401);

    // Should still be on login page
    cy.url().should('include', '/login');

    // Error message should be displayed
    cy.get('[data-cy=login-error]')
      .should('be.visible')
      .and('contain', /invalid|incorrect|failed/i);
  });

  it('should handle network error gracefully', () => {
    cy.intercept('POST', '**/api/v1/auth/login', {
      forceNetworkError: true,
    }).as('loginNetworkError');

    cy.get('[data-cy=username-input]').type(Cypress.env('testUsername'));
    cy.get('[data-cy=password-input]').type(Cypress.env('testPassword'));
    cy.get('[data-cy=login-button]').click();

    cy.get('[data-cy=login-error]')
      .should('be.visible')
      .and('contain', /network|connection|unavailable/i);
  });

  it('should navigate to registration page from login', () => {
    cy.get('[data-cy=register-link]').click();
    cy.url().should('include', '/register');
    cy.get('[data-cy=register-form]').should('be.visible');
  });

  it('should logout successfully', () => {
    // Login first
    cy.login(Cypress.env('testUsername'), Cypress.env('testPassword'));

    cy.intercept('POST', '**/api/v1/auth/logout').as('logoutRequest');
    cy.get('[data-cy=logout-button]').click();

    cy.wait('@logoutRequest').its('response.statusCode').should('eq', 200);

    // Should redirect to login page
    cy.url().should('include', '/login');

    // Verify login form is visible again
    cy.get('[data-cy=login-form]').should('be.visible');
  });
});
