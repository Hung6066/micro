// ***********************************************************
// This is the E2E support file for His.Hope Cypress tests.
// ***********************************************************

import './commands';

// Preserve auth cookies across tests
Cypress.Cookies.defaults({
  preserve: ['access_token', 'refresh_token'],
});

// Log uncaught exceptions for debugging, but don't fail tests on app errors
Cypress.on('uncaught:exception', (err) => {
  console.error('[Cypress] Uncaught exception:', err.message);
  // Return false to prevent Cypress from failing the test
  return false;
});

// Set up request interception for API monitoring
before(() => {
  cy.intercept('**/api/v1/**', (req) => {
    req.on('response', (res) => {
      if (res.statusCode >= 400) {
        console.warn(`[API Warning] ${req.method} ${req.url} -> ${res.statusCode}`);
      }
    });
  });
});
