# Harbor public HTTPS — `harbor.myduchospital.com`

## Hiện trạng đã xác minh

- DNS `harbor.myduchospital.com` → `172.16.102.100`.
- Harbor đang `Running` trong namespace `harbor`; Harbor nginx HTTPS NodePort là `30003`, còn Traefik public HTTPS NodePort là `30443`.
- HAProxy source trước đây bind VIP port `9443`, không phải port `443`; cấu hình mới chuyển public 443 tới Traefik `30443`.
- Endpoint public hiện đã phục vụ certificate có SAN `harbor.myduchospital.com`, issuer `His.Hope Internal Intermediate CA`; đã xác minh bằng `D:\secure\his-hope\vault_pki_ca_chain.pem` và trả HTTP 200. File `harbor_cert.pem` cũ chỉ dành cho hostname legacy và không dùng cho public endpoint.

## Cắt chuyển production an toàn

1. Cấp một certificate từ CA doanh nghiệp/ACME có SAN `harbor.myduchospital.com`, lưu ngoài git:
   - `D:\secure\his-hope\harbor_public_cert.pem`
   - `D:\secure\his-hope\harbor_public_key.pem`
   - nếu dùng chain: `D:\secure\his-hope\harbor_public_chain.pem`
   - CSR đã tạo tại `D:\secure\his-hope\harbor_public.csr.pem`; private key đã tạo tại `D:\secure\his-hope\harbor_public_key.pem`.
   - Không tự ký bằng `His.Hope Local CA` cho public production.
2. Tạo secret `harbor-public-tls` trong namespace `harbor` bằng secret manager hoặc:

   ```powershell
   kubectl -n harbor create secret tls harbor-public-tls `
     --cert D:\secure\his-hope\harbor_public_chain.pem `
     --key D:\secure\his-hope\harbor_public_key.pem
   ```

   Có thể lưu key/chain vào Azure Key Vault sau khi CA cấp chain:

   ```powershell
   pwsh D:\AI\micro\scripts\wrap-harbor-public-tls-azure.ps1
   ```

   Lệnh này yêu cầu quyền Key Vault `secrets/set`; lần kiểm tra hiện tại
   service principal bị `ForbiddenByRbac`, nên chưa ghi secret nào lên Vault.

3. Apply `k8s/harbor/harbor-public-ingress.yaml` sau khi secret tồn tại.
4. Chạy playbook external LB trên cả `.13` và `.14`; biến `lb_harbor_https_port` phải là `443`, backend phải là Traefik public HTTPS `.10:30443` và `.12:30443`. Không dùng Harbor nginx NodePort `30003` cho hostname public vì NodePort đó phục vụ certificate legacy `harbor.his-hope.local`.
5. Nếu Harbor Helm release vẫn terminate TLS ở nginx, cập nhật `externalURL` thành `https://harbor.myduchospital.com` trong values và rollout có kiểm soát. Không đổi URL trước khi certificate, LB và image-pull credentials sẵn sàng.
6. Kiểm thử theo thứ tự:

   ```powershell
   pwsh scripts/validate-harbor-public-https.ps1
   curl.exe -sSIf https://harbor.myduchospital.com/
   docker login harbor.myduchospital.com
   curl.exe -sSf https://harbor.myduchospital.com/api/v2.0/health
   ```

## Không được coi là hoàn tất

Không dùng `harbor-tls` hiện tại cho public hostname, không dùng `-k` để nghiệm thu, và không bật GitOps production sync nếu gate `dns-vip`, `tcp-443`, `tls-secret`, ingress và Harbor health chưa `pass`.
