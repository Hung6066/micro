# Identity Service Penetration Test Plan

## Test Cases

### TC-01: PKCE Bypass
- **Vector:** Attempt authorization code flow without code_challenge
- **Expected:** 400 Bad Request
- **Tool:** curl, Burp Suite

### TC-02: Authorization Code Replay
- **Vector:** Capture valid auth code, submit twice to /connect/token
- **Expected:** Second attempt returns invalid_grant
- **Tool:** curl

### TC-03: CSRF on Authorization Endpoint
- **Vector:** Craft malicious redirect with attacker's redirect_uri
- **Expected:** OpenIddict rejects non-registered redirect_uri
- **Tool:** Browser, Burp Suite

### TC-04: Token Replay from Different IP
- **Vector:** Capture valid access token, replay from different IP
- **Expected:** Token binding check rejects (if introspection used)
- **Tool:** curl from different hosts

### TC-05: Refresh Token Reuse Detection
- **Vector:** Capture refresh token, use it, then reuse the old one
- **Expected:** Entire token family revoked, user must re-authenticate
- **Tool:** curl

### TC-06: Client Impersonation
- **Vector:** Use wrong client_id with valid authorization code
- **Expected:** Token endpoint rejects
- **Tool:** curl

### TC-07: Scope Escalation
- **Vector:** Request scopes not authorized for the client
- **Expected:** Token issued with only authorized scopes
- **Tool:** curl

### TC-08: Brute Force Protection
- **Vector:** Rapid login attempts with wrong password
- **Expected:** Account lockout after 5 attempts
- **Tool:** hydra, ffuf

### TC-09: SQL Injection on SCIM Filter
- **Vector:** Malicious filter parameter on /scim/v2/Users?filter=
- **Expected:** EF Core parameterized query prevents injection
- **Tool:** sqlmap

### TC-10: JWT None Algorithm
- **Vector:** Present JWT with alg=none
- **Expected:** OpenIddict rejects (RS256 only)
- **Tool:** jwt_tool

### TC-11: JWKS Spoofing
- **Vector:** Serve malicious JWKS endpoint
- **Expected:** Issuer validation + introspection prevents
- **Tool:** Custom script

### TC-12: Rate Limiting
- **Vector:** Rapid /connect/token requests
- **Expected:** Rate limited after 30/minute
- **Tool:** ab, wrk

## Test Schedule
1. Automated scans: OWASP ZAP, trivy, snyk
2. Manual testing: OAuth2-specific attack vectors (above)
3. Infrastructure: K8s network policies, mTLS verification

## Success Criteria
- Zero critical/high findings
- All medium findings have remediation plans
- OIDC conformance tests pass
