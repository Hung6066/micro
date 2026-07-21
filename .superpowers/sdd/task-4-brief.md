### Task 4: End-to-end Playwright, build, and Docker verification

**Files:**
- Create: `tests/e2e/specs/13-critical-alerts.spec.js`
- Update only if needed: `tests/e2e/specs/07-lab.spec.js`
- Update only if needed: `docker/docker-compose.yml`

**Interfaces:**
- Consumes: the implemented Lab Service API, Angular UI, and Docker Compose stack.
- Produces: end-to-end evidence that a rule can be created, a critical result can be recorded, realtime notification appears, and the alert can be acknowledged.

- [ ] **Step 1: Write the failing Playwright flow**

Create a spec that asserts:
- a lab-admin or equivalent user can open the critical alert inbox,
- a critical rule can be created,
- recording a critical result shows a toast/badge update,
- the alert can be acknowledged,
- the audit/history panel shows the acknowledgment.

Run:
`npx playwright test tests/e2e/specs/13-critical-alerts.spec.js --workers=1`

Expected: fail until the feature is implemented.

- [ ] **Step 2: Build and start the Docker stack**

Run:
`docker compose -f docker/docker-compose.yml build labservice frontend`

Run:
`docker compose -f docker/docker-compose.yml up -d postgres redis rabbitmq identityservice patientservice clinicalservice labservice apigateway frontend`

Run:
`docker compose -f docker/docker-compose.yml ps`

Expected: labservice, apigateway, and frontend become healthy and the app is reachable on `http://localhost:8081`.

- [ ] **Step 3: Rerun backend, frontend, and Playwright checks**

Run:
`dotnet test tests/Services/LabService/LabService.Domain.Tests/LabService.Domain.Tests.csproj -v normal`

Run:
`dotnet test tests/Services/LabService/LabService.Application.Tests/LabService.Application.Tests.csproj -v normal`

Run:
`dotnet test tests/Services/LabService/LabService.Integration.Tests/LabService.Integration.Tests.csproj -v normal`

Run:
`npm test -- --runInBand --testPathPattern=lab-critical-alert`

Run:
`npx playwright test tests/e2e/specs/13-critical-alerts.spec.js --workers=1`

Expected: all pass.

- [ ] **Step 4: Commit**

Commit message: `test(lab): add end-to-end critical alert verification`

---

