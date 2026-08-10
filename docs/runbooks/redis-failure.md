# Redis Failure Runbook

| Field | Value |
|-------|-------|
| **Severity** | P0–P1 |
| **Service** | Redis (shared cache, session store, JWT blacklist) |
| **Owner** | SRE Team |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `RedisNodeDown` — Redis node unreachable for > 10s
- `RedisMemoryUsageHigh` — `used_memory_peak_perc > 90%`
- `RedisOOM` — Out-of-memory errors in Redis logs
- `RedisSplitBrain` — Sentinel detects multiple masters
- `RedisEvictions` — `evicted_keys > 100/min`

## Symptoms

- **Session loss**: Users redirected to login, JWT tokens rejected
- **Cache misses**: All services report degraded performance, DB load spikes 10x
- **Distributed lock failures**: Background jobs execute concurrently (appointment reminders, outbox processors)
- **Rate limiter bypass**: API rate limiting stops working
- **Jaeger traces**: Redis spans return errors (`MOVED`, `ASK`, `TIMEOUT`, `OOM`)
- **Kibana**: `StackExchange.Redis.ConnectionMultiplexer` errors: `No connection is available`
- **Application logs**: `RedisTimeoutException` or `RedisConnectionException`

## Diagnosis

```bash
# 1. Check Redis pod status
kubectl get pods -n his-hope -l app=redis

# 2. Connect to Redis and check health
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD ping

# 3. Check memory usage
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD INFO memory
# Key fields: used_memory_human, used_memory_peak_human, maxmemory_human

# 4. Check eviction and keyspace stats
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD INFO stats
# Key fields: evicted_keys, keyspace_hits, keyspace_misses

# 5. Check cluster info (if Redis Cluster mode)
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD CLUSTER INFO
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD CLUSTER NODES

# 6. Check replication (if sentinel/HA mode)
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD INFO replication

# 7. List top-10 largest keys by size
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  --bigkeys | Select-Object -First 30

# 8. Check slow log
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD SLOWLOG GET 20

# 9. Check current connections and clients
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD CLIENT LIST
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD INFO clients
# Key fields: connected_clients, blocked_clients, maxclients

# 10. Check OOM kills in pod events
kubectl describe pod redis-0 -n his-hope | findstr "OOMKilled|Memory|Limits"
```

## Mitigation

### A. High Memory / OOM

```bash
# 1. Evict non-critical keys immediately
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  EVAL "return redis.call('DEL', unpack(redis.call('KEYS', 'sessions:*')))" 0
# ⚠ Only evict session keys if acceptable (forces logouts)

# 2. Flush less critical cache namespaces
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  EVAL "return redis.call('DEL', unpack(redis.call('KEYS', 'cache:rate-limit:*')))" 0

# 3. Temporarily reduce TTL on cached data via config change in app ConfigMap
# (Contact @dotnet team to adjust CacheOptions:DefaultTtl)

# 4. Set aggressive eviction policy if not already set
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  CONFIG SET maxmemory-policy allkeys-lru

# 5. Increase Redis maxmemory in Helm values if cluster has headroom
# (Edit k8s/redis/values.yaml: maxmemory: 4Gi → 6Gi)
```

### B. Node Down / Split Brain (Sentinel Mode)

```bash
# 1. Verify which node is the current master
kubectl exec redis-sentinel-0 -n his-hope -- redis-cli -p 26379 SENTINEL get-master-addr-by-name mymaster

# 2. Force a failover if needed
kubectl exec redis-sentinel-0 -n his-hope -- redis-cli -p 26379 SENTINEL failover mymaster

# 3. If split-brain, identify conflicting masters
kubectl exec redis-sentinel-0 -n his-hope -- redis-cli -p 26379 SENTINEL masters
# Look for multiple masters with different runids

# 4. Resolve split-brain by killing the stale master
kubectl exec redis-1 -n his-hope -- redis-cli -a $REDIS_PASSWORD DEBUG SET-ACTIVE-EXEC
kubectl exec redis-1 -n his-hope -- redis-cli -a $REDIS_PASSWORD SHUTDOWN NOSAVE
kubectl delete pod redis-1 -n his-hope

# 5. Sentinel will auto-recover — wait for re-election
watch -n 5 kubectl exec redis-sentinel-0 -n his-hope -- redis-cli -p 26379 SENTINEL master mymaster
```

### C. Connection Exhaustion

```bash
# 1. Check how many connections from each service
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD CLIENT LIST | Group-Object { $_ -replace '.*id=\d+ addr=(\S+).*','$1' }

# 2. Kill idle connections (older than 60s)
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  CLIENT KILL TYPE normal

# 3. Reduce connection pool sizes in affected services (app config)
# Setting: StackExchange.Redis: ConnectionMultiplexer: ConnectTimeout=5000
```

## Resolution

### Root Cause Fix

| Cause | Fix |
|---|---|
| Memory leak (cache keys without TTL) | Add expiry to all `SET` commands; enforce via Redis config `maxmemory-policy` |
| Under-provisioned | Increase `maxmemory` in Helm values; add more cluster shards |
| Sentinel split-brain | Upgrade to Redis Cluster; verify `quorum > (total_nodes/2)` |
| Client connection leak | Fix `ConnectionMultiplexer` lifecycle (must be singleton; not created per-request) |
| OOMKilled | Add memory request/limit to pod spec; enable swap only if kernel allows |

### Verification

```bash
# 1. Redis ping succeeds
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD ping
# → PONG

# 2. Memory below 75%
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD INFO memory | findstr "used_memory_peak_perc"

# 3. No evictions
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD INFO stats | findstr "evicted_keys"

# 4. Cluster state OK (if cluster mode)
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD CLUSTER INFO | findstr "cluster_state"

# 5. Application health checks pass
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf http://localhost:5000/health/ready

# 6. Session login flow works
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -X POST http://localhost:5000/api/v1/auth/login \
    -H 'Content-Type: application/json' \
    -d '{"username":"test-doctor","password":"[REDACTED]"}' | findstr "token"
```

## Postmortem

Use the standard incident postmortem template at `docs/postmortem-template.md`.

Key metrics to capture:
- Memory usage graph (last 24h) showing the ramp
- evicted_keys rate before and after mitigation
- Max connection count at peak
- Sentinel log around failover event

---

> **Last updated**: 2026-07-17 | **Maintainer**: @sre | **Next review**: 2026-09-17
