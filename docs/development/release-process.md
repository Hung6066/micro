# Quy trình Release — His.Hope

> Quy trình phát hành cho hệ thống EMR microservices với CI/CD tự động

---

## 1. Phiên bản (Version Numbering)

Tuân thủ [Semantic Versioning 2.0](https://semver.org/): **MAJOR.MINOR.PATCH**

| Thành phần | Tăng khi | Ví dụ |
|------------|----------|-------|
| **MAJOR** | Breaking change API, thay đổi không tương thích ngược | `2.0.0` |
| **MINOR** | Tính năng mới, backward compatible | `1.3.0` |
| **PATCH** | Bug fix, security patch, backward compatible | `1.2.1` |

### Pre-release tags

```
v1.2.0-rc1    # Release Candidate 1
v1.2.0-rc2    # Release Candidate 2 (re-test)
v1.2.0-beta1  # Beta release cho internal testing
v1.2.0-alpha1 # Alpha release cho team platform
```

---

## 2. Chu kỳ Release (Release Cadence)

| Loại | Tần suất | Mô tả |
|------|----------|-------|
| **Sprint Release** | 2 tuần | Feature batch từ `develop` |
| **Patch Release** | Khi cần | Bug fix quan trọng không gấp |
| **Hotfix Release** | Ngay lập tức | Security vulnerability hoặc critical bug production |

```
Sprint 1         Sprint 2         Sprint 3
├─────┼─────┤    ├─────┼─────┤    ├─────┼─────┤
     │                  │                  │
     ▼                  ▼                  ▼
  v1.1.0            v1.2.0            v1.3.0
  (Thu 2)           (Thu 4)           (Thu 6)
```

---

## 3. Release Pipeline

### 3.1 Tổng quan

```
develop ──▶ feature freeze ──▶ release/v1.2.0 ──▶ test suite ──▶ v1.2.0-rc1
                                                                       │
                                                          ┌────────────┘
                                                          ▼
                                                     staging deploy
                                                     smoke tests
                                                          │
                                                          ▼
                                                     canary (5%)
                                                          │
                                                     canary (25%)
                                                          │
                                                     canary (50%)
                                                          │
                                                     full rollout (100%)
                                                          │
                                                     SLO monitoring (24h)
                                                          │
                                                          ▼
                                                     v1.2.0 (GA tag)
                                                     merge to main
```

### 3.2 Chi tiết từng bước

#### Bước 1: Feature Freeze

```bash
# Ngày feature freeze (thường thứ 2, tuần release)
git checkout develop
git pull origin develop
git checkout -b release/v1.2.0
git push origin release/v1.2.0

# Từ thời điểm này:
# - develop vẫn nhận feature mới cho sprint sau
# - release/v1.2.0 chỉ nhận bug fix và cherry-pick
```

#### Bước 2: Full Test Suite

Pipeline tự động chạy trên branch `release/v1.2.0`:

```yaml
# .github/workflows/release.yml (hoặc cicd/tekton/release-pipeline.yaml)
jobs:
  test:
    steps:
      - name: Unit Tests (.NET)
        run: dotnet test --filter "Category=Unit" --collect:"XPlat Code Coverage"

      - name: Unit Tests (Angular)
        run: |
          cd src/Frontend/his-hope-app
          npm ci
          npx ng test --watch=false --code-coverage

      - name: Integration Tests
        run: dotnet test --filter "Category=Integration"

      - name: E2E Tests
        run: npx cypress run

      - name: Security Scan (secrets)
        uses: gitleaks/gitleaks-action@v2

      - name: Security Scan (dependencies)
        run: dotnet list package --vulnerable

      - name: Code Coverage Check
        run: |
          # Coverage must not drop below baseline
          dotnet reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage
          # Fail if < 80% line coverage
```

#### Bước 3: Tag Release Candidate

```bash
# Sau khi test suite pass
git tag -a v1.2.0-rc1 -m "Release Candidate 1 for v1.2.0"
git push origin v1.2.0-rc1
```

Tag convention:
- `v1.2.0-rc1` — Release candidate, đưa lên staging
- `v1.2.0-rc2` — Release candidate sau khi fix lỗi phát hiện trong staging

#### Bước 4: Staging Deployment + Smoke Tests

```bash
# ArgoCD tự động sync staging environment với tag mới
argocd app set his-hope-staging --revision v1.2.0-rc1
argocd app sync his-hope-staging

# Smoke tests trên staging
dotnet test tests/Smoke/SmokeTests.csproj --filter "Category=Smoke"
```

Smoke test checklist:
- [ ] Health check tất cả services trả về 200
- [ ] Login flow hoạt động (IdentityService)
- [ ] Tạo patient thành công (PatientService)
- [ ] gRPC inter-service calls hoạt động
- [ ] Event bus publish/subscribe hoạt động
- [ ] Database migrations không lỗi
- [ ] Grafana dashboards hiển thị metrics
- [ ] Jaeger traces xuất hiện

#### Bước 5: Production Canary Rollout

Sử dụng Linkerd Traffic Split để canary deploy:

```yaml
# k8s/linkerd/traffic-split.yaml (tạo động từ CI)
apiVersion: split.smi-spec.io/v1alpha4
kind: TrafficSplit
metadata:
  name: patient-service-split
  namespace: his-hope
spec:
  service: patient-service
  backends:
    - service: patient-service-stable   # v1.1.0
      weight: 950m                      # 95%
    - service: patient-service-canary   # v1.2.0
      weight: 50m                       # 5%
```

Quy trình canary:

```
Phase 1:  5% canary → theo dõi 30 phút → SLO OK → tiếp tục
Phase 2: 25% canary → theo dõi 30 phút → SLO OK → tiếp tục
Phase 3: 50% canary → theo dõi 30 phút → SLO OK → tiếp tục
Phase 4: 100% rollout → theo dõi 24h
```

#### Bước 6: SLO Monitoring (24h)

Các SLO cần monitored trong 24h sau full rollout:

| SLO | Target | Cảnh báo |
|-----|--------|----------|
| **Availability** | 99.95% | < 99.9% → rollback |
| **Latency (p95)** | < 200ms REST, < 100ms gRPC | > 500ms → investigate |
| **Error Rate** | < 0.1% | > 1% → rollback |
| **gRPC success rate** | > 99.9% | < 99% → rollback |

```yaml
# k8s/monitoring/slo-exporter-config.yaml
spec:
  slos:
    - name: patient-service-availability
      objective: 99.95%
      indicator:
        ratio:
          good: sum(rate(http_requests_total{service="patient-service",status=~"2.."}[5m]))
          total: sum(rate(http_requests_total{service="patient-service"}[5m]))
```

#### Bước 7: Release GA (General Availability)

```bash
# Sau 24h SLO OK
git tag -a v1.2.0 -m "Release v1.2.0 - Patient Search v2 + Clinical Templates"
git push origin v1.2.0

# Merge release branch vào main
git checkout main
git merge release/v1.2.0
git push origin main

# Merge back vào develop (để đảm bảo không miss hotfix)
git checkout develop
git merge release/v1.2.0
git push origin develop

# Xóa release branch
git branch -d release/v1.2.0
git push origin --delete release/v1.2.0

# ArgoCD sync production
argocd app set his-hope-production --revision v1.2.0
```

---

## 4. Rollback Triggers

### 4.1 Điều kiện rollback

Rollback **tự động** hoặc **thủ công** khi một trong các điều kiện sau xảy ra trong canary phase:

| Trigger | Hành động |
|---------|-----------|
| **SLO breach**: Availability < 99.9% | Auto-rollback về stable |
| **Critical bug**: Lỗi ảnh hưởng đến patient safety/data integrity | Manual rollback ngay |
| **Security incident**: Phát hiện vulnerability mới | Manual rollback + incident response |
| **Error rate spike**: Error rate > 1% kéo dài > 5 phút | Auto-rollback |
| **Database migration failure**: Migration lỗi trên production | Manual rollback + DBA review |

### 4.2 Quy trình rollback

```bash
# Auto rollback qua ArgoCD
argocd app rollback his-hope-production

# Hoặc manual:
argocd app set his-hope-production --revision v1.1.0
argocd app sync his-hope-production

# Linkerd traffic split về 100% stable
kubectl apply -f - <<EOF
apiVersion: split.smi-spec.io/v1alpha4
kind: TrafficSplit
metadata:
  name: patient-service-split
  namespace: his-hope
spec:
  service: patient-service
  backends:
    - service: patient-service-stable
      weight: 1000m
EOF
```

### 4.3 Database Migration Rollback Policy

**Nguyên tắc**: Migration trong His.Hope là **ADDITIVE ONLY** — không destructive changes.

| Migration type | Rollback action |
|----------------|-----------------|
| **ADD COLUMN** | Không cần rollback DB. Ứng dụng cũ bỏ qua column mới. |
| **CREATE TABLE** | Không cần rollback DB. Ứng dụng cũ không query table mới. |
| **CREATE INDEX** | Không cần rollback. Index mới không ảnh hưởng ứng dụng cũ. |
| **DROP COLUMN** (cấm) | Không được phép trong migration. Nếu cần, đánh dấu deprecated, xóa sau 1 sprint. |

```
Tuần 1 (v1.2.0): ADD COLUMN IsArchived BOOL DEFAULT false
                  → Rollback: app cũ bỏ qua column. DB có thêm column vô hại.

Tuần 3 (v1.3.0): SET DEFAULT IsArchived = true (cho new records)
Tuần 4 (v1.3.1): UPDATE existing records SET IsArchived = true
Tuần 5 (v1.4.0): ALTER TABLE DROP COLUMN OldColumn  ← Sau 3 sprint mới được DROP
```

---

## 5. Quy trình Hotfix

### 5.1 Khi nào cần hotfix

- Security vulnerability (critical/high severity)
- Bug gây mất dữ liệu hoặc sai dữ liệu bệnh nhân
- Dịch vụ down hoàn toàn (không thể chờ sprint release)
- Compliance/regulatory issue khẩn cấp

### 5.2 Hotfix Pipeline (Accelerated)

```
main ──────●────────────────────●──────────● (production)
            \                    \        /
hotfix/HH-999 ──●──●──────────────●──────●
                  fix   test     tag    merge
                  (1h)  (30m)   v1.2.1
```

```bash
# 1. Tạo hotfix branch từ main
git checkout main
git pull origin main
git checkout -b hotfix/HH-999-fix-patient-search-leak

# 2. Fix + test
dotnet build && dotnet test --filter "FullyQualifiedName~PatientSearch"

# 3. Tạo tag hotfix
git tag -a v1.2.1 -m "Hotfix v1.2.1 - Fix patient search data leak"
git push origin hotfix/HH-999-fix-patient-search-leak
git push origin v1.2.1

# 4. CI/CD chạy accelerated pipeline:
#    - Unit tests (10 phút)
#    - Security scan (15 phút)
#    - Deploy staging (10 phút)
#    - Smoke test (15 phút)
#    - Canary 5% → 50% → 100% (nhanh hơn: 5 phút mỗi phase)
#    Total: ~1 giờ (so với 24h cho sprint release)

# 5. Merge về main
git checkout main
git merge hotfix/HH-999-fix-patient-search-leak
git push origin main

# 6. Cherry-pick về develop
git checkout develop
git cherry-pick <commit-hash>
git push origin develop

# 7. Xóa hotfix branch
git branch -d hotfix/HH-999-fix-patient-search-leak
git push origin --delete hotfix/HH-999-fix-patient-search-leak
```

### 5.3 Hotfix Merge Strategy

```
         hotfix/HH-999
        /             \
main ──●───────────────●──▶ main (merged) + v1.2.1 tag
        \
         ● cherry-pick
          \
develop ──●──▶ develop (cherry-picked)
```

- **Merge vào main**: Để deploy production ngay
- **Cherry-pick về develop**: Để integration branch có fix, tránh regression ở sprint sau

---

## 6. GitOps với ArgoCD

### 6.1 Environment mapping

| Branch/Tag | ArgoCD App | Environment |
|------------|------------|-------------|
| `develop` | `his-hope-staging` | Staging (auto-sync) |
| `v*-rc*` | `his-hope-staging` | Staging (manual sync) |
| `v*` (GA tag) | `his-hope-production` | Production (manual sync, canary) |

### 6.2 ArgoCD Application

```yaml
# cicd/argo/his-hope-production.yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: his-hope-production
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/your-org/his-hope
    targetRevision: v1.1.0          # Pin to GA tag
    path: k8s/overlays/prod
  destination:
    server: https://kubernetes.default.svc
    namespace: his-hope
  syncPolicy:
    syncOptions:
      - CreateNamespace=true
      - PrunePropagationPolicy=foreground
    automated:
      prune: true
      selfHeal: true
```

### 6.3 Image Promotion

```yaml
# k8s/overlays/prod/image-digests.yaml
images:
  - name: his-hope/patient-service
    newTag: v1.2.0           # Cập nhật khi release GA
    digest: sha256:abc123... # Immutable digest từ CI
```

---

## 7. Release Checklist (Summary)

### Pre-release

- [ ] Tất cả feature cho sprint đã merge vào `develop`
- [ ] Tạo branch `release/vX.Y.Z` từ `develop`
- [ ] Chạy full test suite (unit + integration + E2E)
- [ ] Security scan passed (no secrets, no vulnerable packages)
- [ ] Database migrations verified trên staging
- [ ] Performance benchmarks không regression

### Release Candidate

- [ ] Tag `vX.Y.Z-rc1`
- [ ] Deploy staging
- [ ] Smoke tests passed
- [ ] Business acceptance testing (nếu cần)
- [ ] SLO monitor trên staging không alert

### Canary

- [ ] 5% traffic → SLO OK (30 phút)
- [ ] 25% traffic → SLO OK (30 phút)
- [ ] 50% traffic → SLO OK (30 phút)
- [ ] 100% rollout

### Post-release (24h monitoring)

- [ ] Availability > 99.95%
- [ ] Latency p95 < 200ms
- [ ] Error rate < 0.1%
- [ ] No security alerts
- [ ] Database health check OK
- [ ] RabbitMQ message queue không backlog
- [ ] All Grafana dashboards green

### Final

- [ ] Tag `vX.Y.Z` (GA)
- [ ] Merge `release/vX.Y.Z` → `main`
- [ ] Merge `main` → `develop`
- [ ] Delete `release/vX.Y.Z` branch
- [ ] Update Backstage catalog với version mới
- [ ] Post release notes lên GitHub Releases
- [ ] Retrospective (họp 30 phút sau release)
