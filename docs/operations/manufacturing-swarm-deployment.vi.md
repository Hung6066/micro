# Manufacturing Docker Swarm deployment

Stack riêng nằm ở `docker/swarm/manufacturing-stack.yml`. Stack chỉ quản lý
Identity, Commerce, Content, Manufacturing API và một Manufacturing worker.
PostgreSQL, Redis và RabbitMQ phải là dependency bên ngoài có endpoint ổn định,
backup và replication riêng.

## Bootstrap

1. Push bốn image đã ký lên registry và lấy digest `sha256`.
2. Tạo các Docker Secrets trên Swarm, không ghi secret vào env file:

```powershell
docker secret create manufacturing-postgres-user .\postgres-user.txt
docker secret create manufacturing-postgres-password .\postgres-password.txt
docker secret create manufacturing-rabbitmq-user .\rabbitmq-user.txt
docker secret create manufacturing-rabbitmq-password .\rabbitmq-password.txt
docker secret create manufacturing-vault-jwt .\vault-jwt.txt
```

3. Tạo env file operator-owned từ `docker/swarm/manufacturing.env.example` và
thay toàn bộ digest, hostname, issuer bằng giá trị thật.
4. Validate trước khi deploy:

```powershell
rtk pwsh -NoProfile -File .\scripts\validate-manufacturing-swarm.ps1 `
  -EnvironmentFile .\docker\swarm\manufacturing.env
```

5. Nạp env vào phiên PowerShell rồi deploy:

```powershell
Get-Content .\docker\swarm\manufacturing.env | ForEach-Object {
  if ($_ -match '^\s*([^#][^=]*)=(.*)$') { Set-Item "Env:$($Matches[1].Trim())" $Matches[2].Trim() }
}
docker stack deploy --with-registry-auth -c .\docker\swarm\manufacturing-stack.yml manufacturing
```

`vault-jwt.txt` phải là workload JWT do SPIRE/Vault cấp cho từng role Swarm;
không dùng Vault root token. Stack mặc định dùng `spiffe-jwt`, yêu cầu Vault
HTTPS và `Vault__AllowStaticToken=false`. Khi token hết hạn, rotate Docker
Secret rồi redeploy stack.

## Topology contract

`manufacturingservice` là HTTP API, chạy nhiều replica và tắt background
consumer/outbox/automation. `manufacturing-worker` chạy một replica, sở hữu
consumer, outbox và lifecycle automation. Không scale worker nếu chưa chứng
minh queue semantics, idempotency và database locking cho replica bổ sung.

Kiểm tra runtime:

```powershell
docker stack services manufacturing
docker stack ps manufacturing --no-trunc
Invoke-WebRequest https://manufacturing.example.com/health/ready
Invoke-WebRequest https://manufacturing.example.com/api/v1/manufacturing/recipes
rtk pwsh -NoProfile -File .\scripts\validate-manufacturing-swarm-runtime.ps1 `
  -StackName manufacturing -NetworkName manufacturing_swarm
rtk pwsh -NoProfile -File .\scripts\validate-manufacturing-swarm-runtime.ps1 `
  -StackName manufacturing -NetworkName manufacturing_swarm `
  -ExerciseScaleAndRestart
```

Request cuối phải trả `401` nếu không có bearer token; HTTP `200` của health
không thay thế authenticated business-flow test.

## K8s migration

Image và runtime contract này tương thích với các Deployment/Service/HPA/Job
đã có trong `k8s/base/manufacturing-platform-services.yaml`. Khi chuyển K8s,
map API thành Deployment/HPA, worker thành Deployment riêng, migration thành
Job, Docker Secrets thành Vault/CSI Secret, và overlay network thành Services.
Không map PostgreSQL local volume của Swarm thành StatefulSet production nếu
chưa có storage replication và backup/restore drill.
