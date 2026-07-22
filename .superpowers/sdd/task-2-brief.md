### Task 2: Apply RBAC to 5 Controllers + 1 Hub

**Files:** Modify 6 files — add `using SystemDashboard.Bff.Authorization;` and replace `[Authorize]` with `[Authorize(Roles = DashboardRoles.ReadOnly)]` or `[Authorize(Roles = DashboardRoles.Manage)]` per the spec.

Controllers to modify:
- `Controllers/ResourcesController.cs` — GET: ReadOnly, POST (lifecycle): Manage
- `Controllers/MetricsController.cs` — ReadOnly
- `Controllers/LogsController.cs` — ReadOnly
- `Controllers/TracesController.cs` — ReadOnly
- `Controllers/EnvironmentController.cs` — ReadOnly
- `Hubs/LogStreamHub.cs` — ReadOnly

- [ ] Modify all 6 files, build, commit: `feat(dashboard): apply role-based authorization to dashboard endpoints`

---

