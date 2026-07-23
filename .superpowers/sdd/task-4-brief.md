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

