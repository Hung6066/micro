window.__HISHOPE_RUNTIME_CONFIG__ = window.__HISHOPE_RUNTIME_CONFIG__ || {
  environment: "development",
  contractVersion: "1",
  apiOrigin: window.location.origin,
  oidcAuthority: window.location.origin,
  dashboardBffOrigin: window.location.origin,
};
window.__HISHOPE_CONFIG__ = window.__HISHOPE_CONFIG__ || {
  apiOrigin: window.__HISHOPE_RUNTIME_CONFIG__.apiOrigin,
  oidcAuthority: window.__HISHOPE_RUNTIME_CONFIG__.oidcAuthority,
  production: window.__HISHOPE_RUNTIME_CONFIG__.environment === "production",
};
