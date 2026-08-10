const trimTrailingSlash = (value) => value.replace(/\/$/, '');

module.exports = {
  clinicalUrl: trimTrailingSlash(process.env.E2E_CLINICAL_URL || 'http://localhost:8081'),
  dashboardUrl: trimTrailingSlash(process.env.E2E_DASHBOARD_URL || 'http://localhost:8082'),
  adminUrl: trimTrailingSlash(process.env.E2E_ADMIN_URL || 'http://localhost:8083'),
};
