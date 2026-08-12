# Đồng bộ GitOps sang Git mirror nội bộ

Sau khi promotion PR vào `production` được merge, workflow
`gitops-mirror-sync.yml` đẩy đúng commit production sang nhánh `production`
của Gitea mirror. Workflow chạy trong protected environment `production` và
không dùng `kubectl` để thay đổi cluster.

Cấu hình trong GitHub environment `production`:

- Variable `GITOPS_MIRROR_REPO_URL`: URL HTTPS clone/push của repository Gitea.
- Secret `GITOPS_MIRROR_USERNAME`: tài khoản bot chỉ có quyền push repository.
- Secret `GITOPS_MIRROR_TOKEN`: token bot, giới hạn repository và hết hạn định kỳ.

URL mặc định của triển khai này là:

```text
https://git-mirror.his-hope.local/gitops-admin/micro.git
```

Tạo DNS cho hostname này, cấp secret TLS `git-mirror-tls` bằng CA doanh
nghiệp/Vault CA, và cài CA đó vào trust store của self-hosted runner.

URL không được nhúng username/token. Workflow kiểm tra HTTPS, commit SHA 40
ký tự và dùng `git push --force-with-lease`. Workflow
`gitops-mirror-verify.yml` chạy kế tiếp để đối chiếu mirror và cả 9 Argo
Applications với cùng revision.

Không cấu hình URL service Kubernetes `*.svc.cluster.local` cho runner GitHub
hosted; dùng endpoint HTTPS có tuyến mạng tới Gitea hoặc self-hosted runner
trong mạng quản trị.
