const trimTrailingSlash = (value) => value.replace(/\/$/, '');

module.exports = {
  // Docker Desktop publishes these ports on IPv4; using localhost can resolve
  // to ::1 on Windows and bypass the published listener.
  clinicalUrl: trimTrailingSlash(process.env.E2E_CLINICAL_URL || 'http://127.0.0.1:8081'),
  dashboardUrl: trimTrailingSlash(process.env.E2E_DASHBOARD_URL || 'http://127.0.0.1:8082'),
  adminUrl: trimTrailingSlash(process.env.E2E_ADMIN_URL || 'http://127.0.0.1:8083'),
};
