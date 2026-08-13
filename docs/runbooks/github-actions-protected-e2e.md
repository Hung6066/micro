# Protected authenticated E2E inputs

The authenticated Playwright gate is intentionally fail-closed. Pull requests
skip the protected runtime suite when the inputs are unavailable; protected
push/release workflows run it and fail if any input is missing or unreachable.

Configure these values in the repository's `production` environment:

- Variables: `E2E_CLINICAL_URL`, `E2E_DASHBOARD_URL`, `E2E_ADMIN_URL`
- Secrets: `E2E_AUTH_PROBE_URL`, `E2E_AUTH_TOKEN`

`E2E_AUTH_PROBE_URL` must be an HTTPS API endpoint that accepts the bearer token
and returns a 2xx response for the production test identity. It must not be a
browser login page or a URL that exposes the token in query parameters.

Example (replace values locally; never commit them):

```powershell
gh variable set E2E_CLINICAL_URL --env production --body 'https://clinical.example.internal'
gh variable set E2E_DASHBOARD_URL --env production --body 'https://dashboard.example.internal'
gh variable set E2E_ADMIN_URL --env production --body 'https://admin.example.internal'
gh secret set E2E_AUTH_PROBE_URL --env production
gh secret set E2E_AUTH_TOKEN --env production
```

After configuration, dispatch the protected workflow or push to its protected
branch and confirm the prerequisite step reports all three frontends reachable
and the authenticated probe returning 2xx. If these values cannot be supplied,
the authenticated gate must remain skipped on pull requests and must not be
reported as a production pass.
