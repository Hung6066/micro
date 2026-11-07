# Gói bàn giao bằng chứng đánh giá bảo mật độc lập

Ngày cập nhật: 2026-09-01

Tài liệu này mô tả bằng chứng còn thiếu để đóng production gate của ba giai đoạn. Các file hiện có trong `artifacts/security/` là bằng chứng tự động từ repository và không được dùng thay cho đánh giá độc lập.

## Phạm vi tối thiểu

Assessor độc lập cần đánh giá và phát hành hai báo cáo riêng:

1. OIDC conformance cho authorization code + PKCE, redirect URI exact-match, refresh/revocation/introspection, discovery và RFC 9700.
2. Penetration test cho Identity Service, API Gateway, BFF session, admin/mobile clients, tenant isolation, CSRF, MFA/assurance policy, SCIM và event/audit paths.

## Metadata bắt buộc

Ghi đè các file sau bằng báo cáo thật do assessor phát hành:

- `artifacts/security/oidc-conformance/report.json`
- `artifacts/security/penetration-test/report.json`

Mỗi file phải có các trường:

```json
{
  "assessmentType": "oidc-conformance hoặc penetration-test",
  "evidenceSource": "external-independent",
  "status": "passed",
  "assessor": "tên pháp nhân hoặc assessor",
  "reportUri": "https://...",
  "completedAt": "2026-09-01T00:00:00Z",
  "signature": {
    "algorithm": "cosign hoặc thuật toán được assessor công bố",
    "verified": true,
    "verificationUri": "https://..."
  }
}
```

`reportUri` và `signature.verificationUri` phải là HTTPS và truy cập được bởi release reviewer. Không dùng `example`, URI nội bộ không kiểm chứng được, hoặc chữ ký của pipeline tự đánh giá.

## Xác minh sau khi nhận báo cáo

Chạy từ repository root:

```powershell
./scripts/verify-independent-security-evidence.ps1 -EvidenceRoot artifacts/security
./scripts/validate-enterprise-production-phases.ps1 -Phase all
```

Gate chỉ chuyển sang `pass` khi verifier xác nhận cả hai báo cáo là `external-independent`, có chữ ký đã verify và metadata assessor hợp lệ. Các báo cáo automated hiện tại vẫn phải được giữ để đối chiếu, nhưng không được đổi `evidenceSource` thủ công.

## Trạng thái hiện tại

- Automated RFC 9700/OIDC matrix: pass.
- Automated remediation/security suite: pass.
- Independent signed OIDC assessment: chưa có.
- Independent signed penetration test: chưa có.
- Production sign-off: `environment-blocked` cho đến khi nhận đủ hai báo cáo trên.
