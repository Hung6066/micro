const { chromium } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { clinicalUrl } = require('./config/urls');
const { signInThroughIdentity } = require('./helpers/sso-login');
const { requireE2eCredentials } = require('./config/credentials');

const STORAGE_STATE_FILE = path.join(__dirname, 'fixtures', 'shared-foundation-auth.json');

module.exports = async () => {
  if (process.env.E2E_AUTH_REQUIRED !== 'true') {
    return;
  }

  fs.mkdirSync(path.dirname(STORAGE_STATE_FILE), { recursive: true });
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox', '--disable-setuid-sandbox'] });
  const context = await browser.newContext();
  const page = await context.newPage();

  try {
    const credentials = requireE2eCredentials();
    await signInThroughIdentity(page, clinicalUrl, {
      email: credentials.email,
      password: credentials.password,
    });
    await context.storageState({ path: STORAGE_STATE_FILE });
  } finally {
    await browser.close();
  }
};
