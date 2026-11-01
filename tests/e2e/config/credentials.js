function getE2eCredentials() {
  if (!process.env.E2E_EMAIL || !process.env.E2E_PASSWORD) {
    throw new Error(
      'Authenticated E2E requires E2E_EMAIL and E2E_PASSWORD from local secret storage.',
    );
  }

  return {
    email: process.env.E2E_EMAIL,
    password: process.env.E2E_PASSWORD,
  };
}

function requireE2eCredentials() {
  return getE2eCredentials();
}

function assertE2eCredentials(email, password) {
  if (email && password) return true;
  if (process.env.E2E_AUTH_REQUIRED === 'true') {
    throw new Error(
      'Authenticated E2E requires E2E_EMAIL and E2E_PASSWORD from protected secret storage.',
    );
  }
  return false;
}

module.exports = { getE2eCredentials, requireE2eCredentials, assertE2eCredentials };
