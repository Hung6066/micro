### Task 2: Lab Service API, realtime hub, and alert endpoints

**Files:**
- Modify: `src/Services/LabService/LabService.Api/Program.cs`
- Create: `src/Services/LabService/LabService.Api/Hubs/LabCriticalAlertHub.cs`
- Create: `src/Services/LabService/LabService.Api/Services/HttpCurrentUserContext.cs`
- Create: `src/Services/LabService/LabService.Api/Services/CriticalAlertRealtimePublisher.cs`
- Create: `src/Services/LabService/LabService.Api/Endpoints/CriticalAlertEndpoints.cs`
- Modify: `src/Services/LabService/LabService.Application/UseCases/LabOrders/Commands/RecordLabResultCommand.cs`
- Modify: `src/Services/LabService/LabService.Application/UseCases/LabOrders/Commands/RecordLabOrderResultCommand.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Commands/AcknowledgeCriticalAlertCommand.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Commands/ResolveCriticalAlertCommand.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Queries/GetCriticalAlertsQuery.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Queries/GetCriticalAlertRulesQuery.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Commands/UpsertCriticalAlertRuleCommand.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Commands/DeleteCriticalAlertRuleCommand.cs`
- Create: `tests/Services/LabService/LabService.Integration.Tests/CriticalAlertEndpointsTests.cs`

**Interfaces:**
- Consumes: `ICurrentUserContext`, `CriticalAlertEvaluator`, `IHubContext<LabCriticalAlertHub>`, and the existing lab order/result command handlers.
- Produces: alert rule CRUD endpoints, alert inbox endpoints, `criticalAlertCreated` / `criticalAlertUpdated` / `criticalAlertAcknowledged` / `criticalAlertResolved` socket events, and alert-aware result-recording behavior.

- [ ] **Step 1: Write failing integration tests for the API and hub behavior**

Create tests that assert:
- alert rules can be created and listed,
- recording a critical result creates exactly one persisted alert,
- acknowledging an alert stores the actor and timestamp,
- resolving an alert keeps audit history,
- the realtime publisher emits the expected payload shape for open/acknowledged/resolved updates.

Run:
`dotnet test tests/Services/LabService/LabService.Integration.Tests/LabService.Integration.Tests.csproj --filter "CriticalAlertEndpointsTests" -v normal`

Expected: fail because the endpoints, hub, and command handlers are missing.

- [ ] **Step 2: Implement the API, hub, and command wiring**

Wire `AddSignalR()` and map the hub in `Program.cs`, add the new endpoints, and update the result-recording command handlers so alert evaluation runs on every result save and publishes notifications after persistence succeeds.

Use `Permission:lab.manage` for rule management and `Permission:lab.view` plus runtime ownership checks for acknowledgment and resolution.

- [ ] **Step 3: Rerun the integration tests**

Run:
`dotnet test tests/Services/LabService/LabService.Integration.Tests/LabService.Integration.Tests.csproj --filter "CriticalAlertEndpointsTests" -v normal`

Expected: pass.

- [ ] **Step 4: Commit**

Commit message: `feat(lab): add critical alert api and realtime hub`

---

