# Vault PKI cho Harbor nội bộ

Vault hiện đã có root `pki` và intermediate `pki_int`. Không tạo root mới.
Role `harbor-public` được giới hạn duy nhất cho `harbor.myduchospital.com`.

Trước khi apply:

1. Dùng Vault operator token để xác nhận `pki_int/roles/harbor-public` tồn tại.
2. Tạo Kubernetes auth role `cert-manager` chỉ bind service account `cert-manager` ở namespace `cert-manager`, policy chỉ cho phép `pki_int/sign/harbor-public` và đọc `pki_int/ca_chain`.
3. Dùng `serviceAccountRef` secretless của cert-manager v1.20; không tạo Vault token tĩnh.
4. Điền `caBundle` bằng CA TLS của Vault (base64), không dùng `harbor_cert.pem` local CA.
5. Apply theo thứ tự:

   ```powershell
   kubectl apply -f D:\AI\micro\k8s\security\cert-manager-vault-issuer-harbor.yaml
   kubectl apply -f D:\AI\micro\k8s\harbor\harbor-vault-certificate.yaml
   kubectl -n harbor describe certificate harbor-public
   kubectl -n harbor get secret harbor-public-tls
   ```

6. Chỉ sau khi Certificate `Ready=True`, apply `harbor-public-ingress.yaml` và rollout HAProxy TCP/443.

Nếu thiếu Vault operator token hoặc quyền Kubernetes auth, trạng thái phải là `blocked`, không tạo token/root mới trong git hay trong manifest.
