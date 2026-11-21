# Production Storage Attestation Template

Mẫu này dùng cho platform/storage owner trước khi mở production promotion của
His.Hope. Không điền secret, SAS token, private key hoặc dữ liệu nhạy cảm vào
file này; chỉ ghi metadata, reference tới hệ thống quản lý secret và evidence
artifact đã được lưu trong protected storage.

## 1. Thông tin change và người chịu trách nhiệm

| Trường | Giá trị |
|---|---|
| Change/ticket | `CHG-...` |
| Environment/cluster | `production / ...` |
| Storage owner | `...` |
| Security approver (khác requester) | `...` |
| Evidence bundle URI | `...` |
| Thời điểm kiểm tra UTC | `...` |
| Hạn review tiếp theo | `...` |

## 2. Azure Blob backup destination

| Control | Bằng chứng bắt buộc | Kết quả |
|---|---|---|
| Account đúng owner/subscription | Azure resource ID, subscription và tenant metadata | PASS/FAIL |
| HTTPS/TLS | `httpsOnly=true`, `minimumTlsVersion=TLS1_2` | PASS/FAIL |
| Public access | `allowBlobPublicAccess=false`, container access private | PASS/FAIL |
| Encryption | CMK Key Vault resource ID, key version và rotation policy | PASS/FAIL |
| Infrastructure encryption | `requireInfrastructureEncryption=true` | PASS/FAIL |
| Immutability | Container bật immutable storage with versioning | PASS/FAIL |
| WORM | Immutability policy `Locked`, retention tối thiểu 30 ngày | PASS/FAIL |
| Network | Private endpoint/firewall rule và DNS resolution evidence | PASS/FAIL |
| Credential | SAS scoped `sr=c`, least privilege, expiry/rotation schedule | PASS/FAIL |

Chạy verifier từ protected environment, không truyền secret qua command line và
không in response chứa SAS:

```powershell
rtk python scripts/validate-azure-blob-access.py --env-file <protected-env>
rtk python scripts/validate-azure-blob-retention.py --env-file <protected-env> --minimum-days 30
rtk powershell -File scripts/configure-azure-blob-immutability.ps1 -AccountName <account> -ContainerName <container>
rtk powershell -File scripts/validate-production-storage-attestation.ps1 -AttestationPath <attestation.json> -OutputPath <evidence>/storage-attestation.json
```

`configure-azure-blob-immutability.ps1` mặc định chỉ dry-run. Lock WORM chỉ được
thực hiện qua change đã phê duyệt với `-AllowProduction -Confirmation LOCK-WORM`.

Production go-live workflow nhận file này qua protected environment secret
`PRODUCTION_STORAGE_ATTESTATION_B64`; secret chỉ chứa JSON metadata/evidence
reference, không chứa password, SAS, private key hoặc token. Workflow còn
nhận `PRODUCTION_STORAGE_ATTESTATION_SHA256` (64 ký tự hex) và từ chối
artifact nếu digest không khớp.

## 3. Kubernetes CSI/database

| Control | Bằng chứng bắt buộc | Kết quả |
|---|---|---|
| StorageClass | Tên class, provider, encryption-at-rest và KMS binding | PASS/FAIL |
| Failure domain | Replica/zone/anti-affinity policy | PASS/FAIL |
| Network | DB endpoint private, NetworkPolicy và TLS verification | PASS/FAIL |
| Identity | Workload identity/service account mapping | PASS/FAIL |
| Backup | CNPG ObjectStore Ready và schedule thành công | PASS/FAIL |
| Restore | Restore vào namespace/database cô lập, checksum khớp | PASS/FAIL |
| Snapshot/DR | VolumeSnapshot hoặc provider DR restore evidence | PASS/FAIL |

Evidence runtime phải được lấy bằng protected kubeconfig; không commit
kubeconfig hoặc token vào repository:

```powershell
rtk powershell -File scripts/validate-database-storage-security-contract.ps1 -OutputPath <evidence>/database-storage-security.json
rtk powershell -File scripts/validate-cnpg-backup-platform.ps1 -Context <production-context> -RunBackup
rtk powershell -File scripts/verify-production-backup-restore.ps1 -Context <production-context>
```

## 4. Quyết định gate

Chỉ ký `APPROVED` khi tất cả control ở trên là `PASS`, artifact có timestamp,
requester/approver tách biệt và retention evidence đã được lưu. `FAIL`,
`BLOCKED` hoặc thiếu artifact phải giữ production promotion ở trạng thái
blocked; không dùng risk acceptance không có expiry để bypass.

| Quyết định | Người ký | Thời điểm |
|---|---|---|
| `APPROVED` / `BLOCKED` | `...` | `...` |
