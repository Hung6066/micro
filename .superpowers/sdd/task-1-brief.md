### Task 1: Backend domain model, persistence, and alert evaluation

**Files:**
- Create: `src/Services/LabService/LabService.Domain/Entities/CriticalAlertRule.cs`
- Create: `src/Services/LabService/LabService.Domain/Entities/CriticalAlert.cs`
- Create: `src/Services/LabService/LabService.Domain/Entities/CriticalAlertAuditEntry.cs`
- Create: `src/Services/LabService/LabService.Domain/ValueObjects/CriticalAlertStatus.cs`
- Create: `src/Services/LabService/LabService.Domain/ValueObjects/CriticalAlertTriggerType.cs`
- Create: `src/Services/LabService/LabService.Domain/Repositories/ICriticalAlertRuleRepository.cs`
- Create: `src/Services/LabService/LabService.Domain/Repositories/ICriticalAlertRepository.cs`
- Create: `src/Services/LabService/LabService.Application/Common/Abstractions/ICurrentUserContext.cs`
- Create: `src/Services/LabService/LabService.Application/DTOs/CriticalAlertDtos.cs`
- Create: `src/Services/LabService/LabService.Application/Services/CriticalAlertEvaluator.cs`
- Modify: `src/Services/LabService/LabService.Infrastructure/Persistence/LabDbContext.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Configurations/CriticalAlertRuleConfiguration.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Configurations/CriticalAlertConfiguration.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Configurations/CriticalAlertAuditEntryConfiguration.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Repositories/CriticalAlertRuleRepository.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Repositories/CriticalAlertRepository.cs`
- Modify: `src/Services/LabService/LabService.Infrastructure/DependencyInjection.cs`
- Test: `tests/Services/LabService/LabService.Domain.Tests/CriticalAlertRuleTests.cs`
- Test: `tests/Services/LabService/LabService.Domain.Tests/CriticalAlertTests.cs`
- Test: `tests/Services/LabService/LabService.Application.Tests/CriticalAlertEvaluatorTests.cs`

**Interfaces:**
- Consumes: `LabOrder`, `LabTest`, `LabResult`, current-user identity (`sub`, `fullName` claims via `ICurrentUserContext`), and active `CriticalAlertRule` rows.
- Produces: `CriticalAlertEvaluator.EvaluateAsync(...)`, `CriticalAlertEvaluator.ResolveAsync(...)`, `CriticalAlertRuleDto`, `CriticalAlertDto`, and `CriticalAlertAuditEntryDto`.

- [ ] **Step 1: Write failing domain and evaluator tests**

Create tests that assert:
- a critical flag creates a single open alert,
- a threshold match creates a single open alert,
- a second save of the same critical result updates the existing alert instead of duplicating it,
- a correction to a noncritical value resolves the alert,
- every state change writes an audit entry with actor information.

Run:
`dotnet test tests/Services/LabService/LabService.Domain.Tests/LabService.Domain.Tests.csproj --filter "CriticalAlertRuleTests|CriticalAlertTests" -v normal`

Run:
`dotnet test tests/Services/LabService/LabService.Application.Tests/LabService.Application.Tests.csproj --filter "CriticalAlertEvaluatorTests" -v normal`

Expected: fail because the alert types and evaluator do not exist yet.

- [ ] **Step 2: Implement the alert model and evaluator**

Implement the new alert entities and value objects, wire them into `LabDbContext`, add repository implementations, and register the evaluator and current-user abstraction in dependency injection.

Keep the storage model additive and compatible with the existing `EnsureCreated` bootstrap path; do not introduce a migration system in this iteration.

- [ ] **Step 3: Rerun the backend unit tests**

Run:
`dotnet test tests/Services/LabService/LabService.Domain.Tests/LabService.Domain.Tests.csproj --filter "CriticalAlertRuleTests|CriticalAlertTests" -v normal`

Run:
`dotnet test tests/Services/LabService/LabService.Application.Tests/LabService.Application.Tests.csproj --filter "CriticalAlertEvaluatorTests" -v normal`

Expected: pass.

- [ ] **Step 4: Commit**

Commit message: `feat(lab): add critical alert domain and evaluator`

---

