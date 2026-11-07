# Ma trận triển khai roadmap nâng cấp hệ thống — 2026-09-01

Tài liệu này đối chiếu roadmap 3 phase trong
`docs/superpowers/specs/2026-07-17-system-design-upgrade.md` với source, test
và bằng chứng runtime hiện có. `implemented` chỉ có nghĩa là đã thấy seam và
test/source tương ứng; không đồng nghĩa production-ready.

## Quy ước trạng thái

| Trạng thái | Ý nghĩa |
|---|---|
| implemented | Có implementation và có test/static evidence phù hợp |
| partial | Có một phần implementation, còn thiếu coverage hoặc integration |
| gap | Chưa có implementation đáp ứng đúng yêu cầu roadmap |
| blocked | Cần bằng chứng ngoài repository hoặc môi trường không có trong workspace |

## Phase 1 — Foundation hardening

| Hạng mục | Bằng chứng source | Trạng thái | Thuật toán/điểm kiểm soát cần giữ |
|---|---|---|---|
| Resilience | `His.Hope.Infrastructure/Resilience`, `His.Hope.Resilience`/ServiceDefaults BFF pipeline, `ResilienceConfigurationTests`, `ResiliencePipelineTests` | implemented | Timeout hữu hạn; retry exponential + jitter chỉ cho transient ở generic và HTTP paths; circuit breaker theo dependency |
| Retry/DLQ | `RabbitMQEventBus`, retry queues và dead-letter topology | implemented | Retry bounded theo tier; business 4xx đi DLQ, không retry vô hạn |
| Idempotency | shared idempotency middleware, scoped SHA-256 storage key, Redis/SQL inbox receipts, SQL processing lease migration, Patient projection processed-event ledger, `InboxDeliveryGuard` cho direct consumers, `RedisInboxStoreTests`, `SqlInboxLeasePolicyTests`, `PatientProjectorIdempotencyTests` | partial (gateway + RabbitMQ source/test) | Key scope theo service + subject + tenant + method + endpoint + client key; event receipt dùng atomic SET NX, SQL reclaim lease 10 phút và completed TTL 7 ngày; direct payment/order/provisioning consumers claim trước side effect và release khi lỗi; projection ledger ghi cùng transaction với read model; cần rollout/migration và consumer coverage đầy đủ |
| Schema/envelope | `His.Hope.Contracts.Messaging`, `EventSchemaRegistry`, explicit RabbitMQ event-type registry từ `SubscribeAsync<TEvent,...>`, publisher/consumer metadata và transport validation | partial | Validate version, audience, tenant, correlation, priority allow-list và aggregate version trước side effect; registry CLR explicit đã loại bỏ assembly scan, còn thiếu schema registry tương thích có version được quản trị tập trung |
| Bulkhead/concurrency | adaptive resilience strategy, per-dependency limiter registry và operator max actions | implemented (source/test) | Concurrency gate theo dependency; ghi latency runtime vào rolling p99; queue bounded, reject khi saturation |
| MFA/BAA/supply chain | Identity security suites, Vault/Cosign/Gatekeeper contracts | partial | MFA là policy theo assurance; BAA và chữ ký external là governance evidence, không thể giả lập trong source |
| Capacity baseline | load baseline artifact và HPA/PDB manifests | implemented | Sizing theo p95/p99, saturation và queue lag; không scale chỉ theo CPU |

Residual Phase 1 cần làm sâu: chuẩn hóa idempotency receipt cho mọi consumer,
đưa mutation còn synchronous về transaction + Outbox, và bổ sung SLO rule
định danh cho từng service. Đây là code work tiếp theo, không được che bằng
việc tăng replica.

## Phase 2 — Structural resilience

| Hạng mục | Bằng chứng source | Trạng thái | Thuật toán/điểm kiểm soát cần giữ |
|---|---|---|---|
| PHI masking/redaction | `PhiDestructuringPolicy`, BFF audit redaction, audit endpoint redaction | implemented | Deny-by-default theo field name; redact trước Serilog/export; không log raw identifier |
| Multi-level cache | shared caching seams, authorization cache partitioning, Redis invalidation bus và `RedisCacheInvalidationBusTests` | implemented (source/test) | L1 bounded + L2 Redis; cache key gồm tenant/subject/policy version; stampede protection bằng single-flight; cross-replica prefix invalidation qua Redis pub/sub |
| FHIR/HL7 gateway | `FhirGateway` service và HL7 references | implemented | Validate profile/version; map lỗi provider thành ProblemDetails; không bypass tenant/patient authorization |
| Distributed lock | `RedisLockManager` và fencing token | implemented | TTL + fencing token; owner kiểm tra token trước commit; lock không thay thế transaction |
| Lifecycle/archive | DataLifecycle services/migrations | implemented | Retention theo policy; archive trước purge; legal hold thắng retention |
| Synthetic/anomaly monitoring | observability and monitoring contracts | partial | Baseline theo rolling window; cảnh báo theo deviation + minimum sample, không dùng một spike đơn |
| Secret rotation | Vault/secret provider seams | partial | Dual-key overlap: issue bằng key mới, verify key cũ trong grace period, revoke sau drain |

Residual Phase 2 là synthetic journeys chạy trong môi trường deployed. Static
manifest hoặc service healthy không đủ để đánh dấu gate runtime này pass.

## Phase 3 — Advanced capabilities

| Hạng mục | Bằng chứng source | Trạng thái | Thuật toán/điểm kiểm soát cần giữ |
|---|---|---|---|
| Persistent Saga | `PersistentSagaOrchestrator`, EF saga state store, Manufacturing migrations | implemented | State transition optimistic-concurrency; timeout/recovery; step idempotent |
| QoS/priority bulkhead | `PriorityAdmissionController`, bounded per-process queue, reserve capacity, aging, K8s PriorityClass, canonical RabbitMQ priority header | partial (source/test) | Reserve capacity cho P0/P1; effective priority có aging chống starvation; queue bounded + timeout; envelope và legacy/new RabbitMQ publisher reject/propagate priority allow-list; cần priority-aware queue/bulkhead runtime theo downstream dependency |
| RUM/Web Vitals | frontend `rum.service.ts`, Web Vitals | implemented | Sample adaptive; gắn release/build/route; không thu thập PHI trong telemetry |
| Webhook engine | HR webhook authentication/delivery paths | partial | HMAC + timestamp window + constant-time compare; retry có backoff; dedup event id |
| PHI de-identification | redaction/pseudonym-related helpers | partial | Tokenization/pseudonymization tách secret; k-anonymity không dùng thay cho access control |
| Feature flags | `IFeatureFlagService`, `UnleashFeatureFlagService`, shared `AddFeatureFlags` composition seam và Unleash manifest | implemented (config-gated) | Production chỉ fail-closed khi bật `FeatureManagement:Required=true`; khi bật phải khai báo URL/app name explicit; provider failure chỉ fallback theo flag default; không tự khởi tạo client khi capability không được bật |
| Enhanced audit hash chain | `AuditLogIntegrity`, hash fields, monotonic `IntegritySequence`, append-only preparation in `IdentityDbContext`, PostgreSQL transaction advisory lock, EF migrations và tamper/concurrency tests | partial | Canonical serialization + SHA-256 chain đã bật cho entry mới; cần backfill/verification job cho legacy rows |
| Auto-remediation operator | `src/Services/RemediationOperator`, CRD/RBAC, scale/restart/rollback/notify | implemented | Allow-list policy, cooldown, bounded concurrency, dry-run, audit CRD, webhook auth |
| Graceful degradation/backpressure | resilience, fallback and backpressure seams | partial | Admission control theo deadline/budget; stale-read/fallback chỉ cho non-critical reads; fail closed với auth/PHI writes; provider chỉ expose đường async để không tạo kết quả kiểm tra cache giả đồng bộ |

## Các thuật toán scale ưu tiên

1. **Adaptive concurrency limit:** mỗi dependency có rolling p99 latency riêng;
   giảm giới hạn 10% khi p99 vượt baseline 20%, tăng 5% khi thấp hơn 90%, với
   giới hạn min/max và queue bounded. Runtime strategy ghi sample sau mỗi lần
   thực thi; lỗi/timeout vẫn được đo qua outcome latency. Không dùng một limiter global.
2. **Queue-based autoscaling:** HPA/KEDA nên dùng queue lag, age của oldest
   message và throughput; CPU chỉ là tín hiệu phụ. Target phải đặt sao cho
   `arrival_rate < service_rate` ở steady state.
3. **Cache single-flight:** một request làm refresh cho mỗi key, request còn
   lại đọc kết quả hoặc stale value trong một khoảng ngắn; negative caching có
   TTL rất ngắn để tránh poisoning.
4. **Idempotent event processing:** ghi receipt và side effect trong cùng
   transaction; duplicate delivery trả kết quả đã biết, không chạy lại side
   effect.
5. **Priority queue có fairness:** reserve capacity cho critical traffic,
   nhưng aging low-priority item để tránh starvation; mọi drop/reject phải có
   metric và correlation id.

## Kết quả kiểm chứng hiện tại

- **Latest current full matrix (2026-09-02T10:34:14Z):** `pass`, 18/18
  projects, 1.828 passed, 0 failed, 0 skipped. Dedicated Manufacturing
  tenant-routing evidence was enabled through the local PostgreSQL
  `manufacturing_customer_acme` database; this supersedes all earlier
  environment-blocked and pass-with-skips matrix snapshots.
- **Latest enterprise phase validator (2026-09-02T10:37:04Z):**
  `environment-blocked` only at signed external-independent OIDC/pentest
  evidence; service integration matrix, release contracts, SIEM tamper drill,
  HA/data-plane, observability, threat model, JWKS rotation, FAPI, SCIM,
  multi-region and legacy-auth checks passed.
- **Latest validator with RFC 9700 integration enabled (2026-09-02T10:40:11Z):**
  RFC 9700 conformance `9/9` passed and the fresh full service matrix remained
  `pass` (`1.828/1.828`, no skips); aggregate status remains
  `environment-blocked` only because signed independent OIDC/penetration-test
  evidence is external to this workspace. Load baseline was intentionally not
  rerun because the existing measured baseline is tracked separately.
- **Latest complete internal validator with load baseline (2026-09-02T10:43:24Z):**
  RFC 9700 `9/9`, full service matrix `1.828/1.828`, load baseline, release,
  HA/data-plane, observability and all remaining internal security checks
  passed. Aggregate remains blocked only by the required signed independent
  OIDC and penetration-test reports.

- Full test matrix: 18/18 projects, 1.817 passed, 2 skipped, 0 failed (2026-09-01T18:40:54Z; sau generic transient-only retry predicate và priority allow-list trong envelope/legacy-new RabbitMQ publishers).
- Shared infrastructure tests: 62/62 passed, gồm cache invalidation, idempotency scope, Redis inbox receipt, SQL inbox lease policy, priority fairness, envelope priority allow-list và transient-only retry.
- Enterprise phase validator: environment-blocked only at `pentest-evidence`; DPoP, RFC9700, service integration, DR contracts, release contracts, SIEM tamper, tenant context, threat model, assurance, JWKS rotation, load baseline, FAPI, SCIM, multi-region và legacy-auth đều pass (2026-09-01T23:35:51Z). Production release validation cũng kiểm tra Vault Shamir dùng đúng `secret_threshold = 3` trong cả config local và StatefulSet.
- Patient projection idempotency: 1/1 PostgreSQL integration test passed; full Patient integration project bị environment-blocked ở 5 endpoint tests do thiếu runtime `EventBus:Password` (6 tests pass).
- Release build: pass, 0 warning, 0 error for the current solution build.
- Manufacturing analytics consumer now requires canonical transport headers and a valid `eventId`, uses the shared inbox guard before persisting receipts, and releases the claim on failure; infrastructure build passed, while the subsequent full Manufacturing suite was environment-blocked by a testhost crash without assertion output.
- Validation correction after direct-consumer inbox guard changes: full matrix is 18/18 projects, 1.818 passed, 2 skipped, 0 failed (2026-09-01T23:39:44Z); shared infrastructure is 64/64 passed, including `InboxDeliveryGuard` tests. This supersedes the earlier 1.817/62 figures above.
- Latest enterprise production-phase validator: `environment-blocked` only at `pentest-evidence`; all other 18 checks passed, including service integration matrix, RFC 9700, DPoP, DR/release contracts, SIEM tamper, threat model, JWKS rotation, load baseline, FAPI, SCIM, multi-region and legacy-auth deprecation (2026-09-01T23:55:57Z).
- Additional messaging hardening: `TenantProvisioningConsumer`, payment-command and payment-authorized queues now use passive-preserving DLX declaration and bounded prefetch; Tenant Provisioning also uses `AsyncEventingBasicConsumer`. Identity, Commerce and Billing infrastructure builds passed with 0 warnings/errors.
- Adaptive concurrency validation: `ResilienceConfigurationTests` 3/3 passed; per-dependency registry, runtime latency recording and bounded adaptive gate are covered. Shared Infrastructure Release build passed with 0 warnings/errors.
- Latest post-change full-matrix attempt: environment-blocked at project 3/18 (`IdentityService.IntegrationTests` testhost exited with code 1 after container startup, failed=0 and no assertion summary); the previously recorded 18/18 pass remains valid only for the pre-adaptive-strategy revision and is not re-used as current proof.
- Evidence freshness correction: the aggregate validator now requires service integration evidence to have a valid `generatedAtUtc` no older than 24 hours. The checked-in matrix generated at `2026-08-29T07:40:49Z` is therefore `environment-blocked`; a fresh RFC 9700 run passed its assertions but the TestHost process was subsequently aborted by workspace contention. This supersedes the service-matrix portion of the earlier aggregate pass claim.
- Frontend production build: pass for all 8 applications on 2026-09-02, with existing non-fatal Angular CommonJS/export-condition warnings retained for a later dependency cleanup.
- Fresh complete matrix attempts: `environment-blocked` at Identity integration because concurrent Identity testhost processes locked the shared project output DLLs (`MSB3021/MSB3027`); a `-NoBuild` attempt reached the test process but was interrupted before a summary while another Identity test tree was active. The runner now preserves this distinction and remains fail-closed in `-RequireComplete` mode (latest evidence 2026-09-02T00:20:31Z).
- CI matrix hardening: the protected full-matrix job now builds the solution once and invokes the serial runner with `-NoBuild`, preventing testhost execution from overlapping with per-project MSBuild copy operations; the workflow contract verifies this sequence.
- Matrix output isolation: the runner and protected CI job now use SDK `UseArtifactsOutput=true` with a shared `ArtifactsPath`, giving every project separate `bin/<project>` and `obj/<project>` trees. An isolated Identity build passed with 0 warnings/0 errors, and the ApiGateway portability contract passed 12/12; a subsequent full run reached the Identity testhost but had no terminal summary while unrelated direct Identity/Manufacturing testhosts were active, so it remains environment-blocked.
- Full-solution isolated build probe: completed with 0 errors and 43 existing compiler/analyzer warnings; this supersedes any earlier local statement that the current solution build had zero warnings.
- Isolated full-matrix follow-up: ApiGateway contract tests passed 12/12 from the isolated artifact tree. Identity integration started Testcontainers and exercised authenticated endpoints, but the host terminated before a TRX/summary while unrelated direct Identity/Manufacturing testhost processes were present; no green claim is made.
- Latest isolated runner retry: ApiGateway and FHIR contract projects passed 19/19 tests; Identity integration again terminated after container startup/request execution with `exit=1`, `failed=0`, no assertion summary and no TRX (`2026-09-02T00:46:12Z`). This is recorded as environment-blocked, not as a test failure; focused Identity tests and shared-infrastructure tests remain separately evidenced.
- Latest supervised full-matrix attempt: runner reached the Identity integration project and executed 630 tests with 0 failed/0 skipped, but terminated before writing a terminal aggregate (`status=in-progress`, `2026-09-02T01:39:51Z`). This artifact is not a green result and remains environment-blocked/unverified.
- Latest resumed full-matrix attempt: reached 1.294 accumulated tests with 0 failed/0 skipped, but was stopped after no progress while the workspace volume fell to 0.21 GB; no terminal aggregate was written. This run is environment-blocked/unverified and its temporary build tree was removed after process-tree termination.
- Focused follow-up after removing stale testhost trees: `AdminIncidentEndpointTests` passed 7/7 with fresh PostgreSQL/Redis containers; this confirms the affected Identity endpoint path is green in isolation, while it does not replace the required complete Identity/full-matrix evidence.
- Full-matrix runner hardening: removed duplicate per-project execution and increased the finite project timeout from 10 to 30 minutes; the runner now uses one isolated `dotnet` process per project and preserves timeout/exit distinctions. A clean rerun reached Identity and later service projects, but the host still terminated before writing a valid aggregate artifact; full-matrix status remains unproven.
- Runner evidence hardening: aggregate `full-test-matrix.v1` is now checkpointed after every completed project, so external termination cannot erase already-collected results; PowerShell parse and runner contract checks pass.
- Authenticated Playwright: blocked in this workspace because `E2E_EMAIL` and `E2E_PASSWORD` are not available; prior 125-pass artifact is not treated as current runtime evidence.
- Load baseline: 523.170 requests, 200 VU, error rate 0, p95 khoảng 21,5 ms.
- Independent OIDC/pentest signed evidence: **blocked**; repository reports là
  automated evidence, không được nâng cấp thành independent assessment.
- RabbitMQ type-resolution hardening (2026-09-02): consumer deserialize theo
  event type đã đăng ký tại `SubscribeAsync<TEvent,...>`, reject mapping trùng
  routing key khác CLR type, không còn scan toàn bộ assemblies; build và
  `EventBusSecurityTests` pass. Đây là hardening type boundary, chưa thay thế
  schema registry compatibility runtime.

## Quyết định triển khai

Không tạo thêm generic repository hoặc service manager. Các thay đổi tiếp theo
phải đi theo vertical slice hiện có: hoàn tất consumer idempotency/Outbox,
bulkhead theo downstream dependency, adaptive limiter có telemetry thực tế,
cache invalidation, rồi bổ sung verifier/backfill cho audit hash chain trước
khi bật production flag.

### Release-evidence wiring correction (2026-09-02)

Full service matrix giờ xuất `full-test-matrix.v1` với `generatedAtUtc` và
`totals.failed/skipped`, tương thích với freshness/green check của enterprise
validator. Job `backend-contracts` phụ thuộc vào `full-service-integration-matrix`,
tải artifact mới và truyền rõ `-IntegrationMatrixPath`; vì vậy validator không
còn có thể vô tình dùng `integration-test-matrix.json` cũ trong workspace.
`SkipIntegrationTests` và `SkipServiceIntegrationMatrix` cũng đã tách nghĩa:
skip RFC/conformance không skip service-matrix freshness gate. Contract tests
và YAML parse đã pass; runtime CI vẫn phải tạo được matrix xanh mới để gate pass.

Full matrix runner tiếp tục được harden bằng child-process supervision: mỗi
project chạy trong process riêng, có timeout 30 phút và cleanup process tree khi
timeout. Điều này ngăn testhost crash làm mất aggregate evidence. Local full
matrix vẫn chưa được nâng thành pass vì D: hết dung lượng trong probe trước và
workspace còn các testhost chạy ngoài runner; CI clean runner là evidence chính.
