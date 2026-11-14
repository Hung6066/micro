window.__HISHOPE_RUNTIME_CONFIG__ = window.__HISHOPE_RUNTIME_CONFIG__ || {
  environment: /^(localhost|127\.0\.0\.1)$/.test(window.location.hostname) ? "development" : "production",
  contractVersion: "1",
  apiOrigin: "https://identity-api.nacoms.com.vn",
  oidcAuthority: "https://identity-api.nacoms.com.vn/",
};
window.__HISHOPE_CONFIG__ = window.__HISHOPE_CONFIG__ || {
  apiOrigin: window.__HISHOPE_RUNTIME_CONFIG__.apiOrigin,
  oidcAuthority: window.__HISHOPE_RUNTIME_CONFIG__.oidcAuthority,
  production: window.__HISHOPE_RUNTIME_CONFIG__.environment === "production",
  clientId: "his-hope-admin",
};
