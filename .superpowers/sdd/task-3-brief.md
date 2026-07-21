### Task 3: Angular lab inbox, rule editor, and realtime client

**Files:**
- Modify: `src/Frontend/his-hope-app/package.json`
- Create: `src/Frontend/his-hope-app/src/app/core/models/critical-alert.model.ts`
- Create: `src/Frontend/his-hope-app/src/app/core/models/critical-alert-rule.model.ts`
- Create: `src/Frontend/his-hope-app/src/app/core/models/critical-alert-audit.model.ts`
- Create: `src/Frontend/his-hope-app/src/app/core/services/lab-critical-alert.service.ts`
- Create: `src/Frontend/his-hope-app/src/app/core/services/lab-critical-alert-stream.service.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/services/lab.service.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/lab/lab.routes.ts`
- Create: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alerts/lab-critical-alerts.component.ts`
- Create: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alert-rule-form/lab-critical-alert-rule-form.component.ts`
- Create: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alert-detail/lab-critical-alert-detail.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/lab/lab-order-list/lab-order-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/lab/lab-order-detail/lab-order-detail.component.ts`
- Test: `src/Frontend/his-hope-app/src/app/core/services/lab-critical-alert.service.spec.ts`
- Test: `src/Frontend/his-hope-app/src/app/core/services/lab-critical-alert-stream.service.spec.ts`
- Test: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alerts/lab-critical-alerts.component.spec.ts`
- Test: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alert-rule-form/lab-critical-alert-rule-form.component.spec.ts`
- Test: `src/Frontend/his-hope-app/src/app/features/lab/lab-order-detail/lab-order-detail.component.spec.ts`

**Interfaces:**
- Consumes: the new lab critical alert REST endpoints and the SignalR hub, plus `AuthService.getStoredAccessToken()` for socket authentication.
- Produces: alert inbox UI, rule editor UI, realtime badge/toast updates, and alert-detail acknowledgment controls.

- [ ] **Step 1: Write failing Jest tests for the alert services and components**

Create tests that assert:
- the service calls the new alert endpoints,
- the socket service subscribes to `criticalAlertCreated` and increments unread state,
- the inbox renders open/acknowledged/resolved filters,
- the rule form validates threshold fields,
- the lab order detail page shows critical-state metadata and an acknowledge action.

Run:
`npm test -- --runInBand --testPathPattern=lab-critical-alert`

Expected: fail because the new components, services, and SignalR client do not exist yet.

- [ ] **Step 2: Implement the Angular feature**

Add the SignalR client dependency, create the alert models/services/components, wire the new lab route, and surface the alert badge and detail actions in the existing lab UI.

Keep the styling aligned with the current Angular Material patterns; do not redesign the lab module shell.

- [ ] **Step 3: Rerun the Jest suite and the Angular build**

Run:
`npm test -- --runInBand --testPathPattern=lab-critical-alert`

Run:
`npm run build`

Expected: both pass.

- [ ] **Step 4: Commit**

Commit message: `feat(lab-ui): add critical alert inbox and realtime updates`

---

