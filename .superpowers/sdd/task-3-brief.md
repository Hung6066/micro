### Task 3: SLO/SLI Dashboard Page

**Backend:**
- Create `src/Bff/SystemDashboard.Bff/Controllers/SloController.cs` — GET `/api/slo` returns SLO data from Prometheus recording rules

**Frontend:**
- Create `src/app/features/slo/slo-page.component.ts` — SLO overview page
- Create `src/app/core/services/slo.service.ts` — calls `/api/slo`
- Add route in `app.routes.ts`: `{ path: 'slo', component: SloPageComponent }`
- Add nav item in `app.component.html`
- Show: availability gauge per service, error budget burn rate, latency p99 sparklines

---

