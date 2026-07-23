### Task 2: Real-time Metrics via SignalR

**Backend:**
- Create `src/Bff/SystemDashboard.Bff/Hubs/MetricsHub.cs` — pushes MetricSnapshot on interval
- Register in `Program.cs`: `app.MapHub<MetricsHub>("/ws/metricshub").RequireAuthorization();`

**Frontend:**
- Create `src/app/core/services/metrics-stream.service.ts` — SignalR connection to `/ws/metricshub`
- Modify `src/app/features/metrics/metrics-page.component.ts` — subscribe to real-time updates
- Modify `src/app/features/resources/resources-page.component.ts` — live CPU/memory badges

---

