# Dashboard Frontend — 7 Enterprise Features Implementation Plan

**Goal:** Add real-time metrics, dark mode, SLO page, alert panel, dependency graph, advanced log search, health timeline to Angular 19 dashboard-app.

**Architecture:** New SignalR metrics hub (backend) + 6 new frontend components/pages. Dark mode via CSS custom properties. All features use existing backend APIs where possible. New backend endpoint needed for alerts (aggregates Prometheus alerts).

**Tech Stack:** Angular 19 standalone components, Material M3, recharts (lightweight charts), SignalR, CSS custom properties

## Global Constraints
- All components use `standalone: true`, inline templates, OnPush change detection
- Existing Material M3 theme (`_theme.scss`) must be extended, not replaced
- Mobile-first: every component must work at 360px width
- Backend changes: 1 new SignalR hub + 1 new controller endpoint only
- No new npm packages except `recharts` (already popular, lightweight charting)

---

### Task 1: Dark Mode + Mobile Responsive

**Files:**
- Modify `src/app/app.component.ts` — add theme toggle + media query
- Modify `src/app/app.component.html` — dark mode toggle button in toolbar
- Modify `src/app/app.component.scss` — responsive sidebar (collapse on mobile)
- Modify `src/styles/_theme.scss` — dark theme variant
- Modify `src/index.html` — class-based theme switching

**Implementation:**
- CSS custom properties for colors (light + dark map)
- `mat-sidenav` mode changes to `over` on mobile (<768px)
- Toggle button in toolbar: `brightness_6` icon, persists to localStorage
- Dark theme: dark backgrounds (#1a1a2e), muted text, reduced brightness on cards

---

### Task 2: Real-time Metrics via SignalR

**Backend:**
- Create `src/Bff/SystemDashboard.Bff/Hubs/MetricsHub.cs` — pushes MetricSnapshot on interval
- Register in `Program.cs`: `app.MapHub<MetricsHub>("/ws/metricshub").RequireAuthorization();`

**Frontend:**
- Create `src/app/core/services/metrics-stream.service.ts` — SignalR connection to `/ws/metricshub`
- Modify `src/app/features/metrics/metrics-page.component.ts` — subscribe to real-time updates
- Modify `src/app/features/resources/resources-page.component.ts` — live CPU/memory badges

---

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

### Task 4: Alert Notification Panel

**Backend:**
- Create `src/Bff/SystemDashboard.Bff/Controllers/AlertsController.cs` — GET `/api/alerts` queries Prometheus AlertManager API
- Create `src/Bff/SystemDashboard.Bff/Hubs/AlertHub.cs` — pushes new/cleared alerts

**Frontend:**
- Create `src/app/core/services/alert.service.ts` — polls `/api/alerts` every 15s or subscribes via SignalR
- Create `src/app/shared/alert-panel/alert-panel.component.ts` — dropdown panel with list of active alerts
- Create `src/app/shared/alert-toast/alert-toast.service.ts` — toast notifications for new alerts
- Modify `app.component.html` — add bell icon with badge count in toolbar

---

### Task 5: Service Dependency Graph

**Frontend only (no backend changes — data from existing resource API):**
- Create `src/app/features/resources/dependency-graph.component.ts` — interactive SVG/Canvas graph
- Add tab or toggle in resources page to switch between card view and graph view
- Graph nodes: services (colored by health), databases, infra
- Graph edges: derived from `databases[]` field on ServiceResource + known dependencies

---

### Task 6: Log Search Enhancement

**Frontend only:**
- Modify `src/app/features/logs/logs-page.component.ts` — add time range picker, traceId link, syntax highlighting
- Create `src/app/shared/time-range-picker/time-range-picker.component.ts` — Material date range picker
- Add quick presets: "Last 5m", "Last 15m", "Last 1h", "Last 24h"
- Log level color coding: ERROR=red, WARN=orange, INFO=blue, DEBUG=gray
- Clickable traceId → navigates to trace detail page

---

### Task 7: Health Timeline

**Frontend only:**
- Create `src/app/features/resources/health-timeline.component.ts` — timeline chart
- Show 24h uptime/downtime per service as horizontal stacked bar
- Data: Poll `/api/resources` every 30s, accumulate health transitions
- Material chip to show current incident count
