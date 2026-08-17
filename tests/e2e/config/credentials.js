const DEFAULT_EMAIL = 'admin@hishop.com';
const DEFAULT_PASSWORD = 'Test@123456';

function getE2eCredentials() {
  return {
    email: process.env.E2E_EMAIL || DEFAULT_EMAIL,
    password: process.env.E2E_PASSWORD || DEFAULT_PASSWORD,
  };
}

function requireE2eCredentials() {
  const credentials = getE2eCredentials();
  if (process.env.E2E_AUTH_REQUIRED === 'true' && !process.env.E2E_PASSWORD) {
    throw new Error('E2E_AUTH_REQUIRED=true requires E2E_PASSWORD from local secret storage.');
  }
  return credentials;
}

module.exports = { getE2eCredentials, requireE2eCredentials };
