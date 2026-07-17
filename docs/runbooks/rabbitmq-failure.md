# RabbitMQ Failure Runbook

| Field | Value |
|-------|-------|
| **Severity** | P0–P1 |
| **Service** | RabbitMQ (event bus, outbox, async processing) |
| **Owner** | SRE Team |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `RabbitMQQueueOverflow` — Queue depth > 10,000 messages for > 5m
- `RabbitMQConnectionExhaustion` — Active connections > 80% of max
- `RabbitMQNodeDown` — RabbitMQ node unreachable for > 10s
- `RabbitMQDeadLetterQueueNotEmpty` — DLQ has messages > 1,000 for > 5m
- `RabbitMQMemoryHighWatermark` — Memory used > 80% of high watermark
- `RabbitMQDiskFreeLow` — Free disk space < 5GB on RabbitMQ node

## Symptoms

- **Outbox processor backlog**: Domain events not delivered to consumers
- **Appointment reminders not sent**: Patients not notified of upcoming appointments
- **Sync failures**: Cross-service data inconsistent (e.g., patient created but billing not configured)
- **Jaeger traces**: Publisher spans succeed but consumer spans never appear
- **Kibana**: `RabbitMQ.Client.Exceptions.OperationInterruptedException` or `BrokerUnreachableException`
- **Grafana**: `rabbitmq_queue_messages_ready` spikes on critical queues
- **API errors**: 503 responses when services cannot publish events
- **PagerDuty**: `RabbitMQMemoryHighWatermark` + `RabbitMQDiskFreeLow` simultaneously

## Diagnosis

```bash
# 1. Check RabbitMQ pod status
kubectl get pods -n his-hope -l app=rabbitmq

# 2. Check node health and resource usage
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl status
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmq-diagnostics check_running

# 3. List queues with message counts
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_queues \
  name messages messages_ready messages_unacknowledged consumers memory

# 4. Check connections and channels
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_connections \
  name user host port state channels
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_channels

# 5. Check memory and disk alarms
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_connections \
  name user state | findstr "blocked"
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl eval 'rabbit_disk_monitor:check().'
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl eval 'rabbit_memory_monitor:check().'

# 6. Check dead letter queues
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_queues \
  name messages consumers | findstr "dlq\|dead.letter\|parking.lot"

# 7. List exchanges and bindings
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_exchanges
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_bindings

# 8. Check consumer acknowledgements
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_consumers

# 9. Get a sample of messages from the overflow queue
kubectl exec his-hope-rabbitmq-0 -n his-hope -- \
  rabbitmqadmin get queue=his-hope.patient.patient_created requeue=true count=3

# 10. Check logs for the failing node
kubectl logs his-hope-rabbitmq-0 -n his-hope --tail=50
```

## Mitigation

### A. Queue Overflow — Slow Consumers

```bash
# 1. Identify the slow consumer
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_consumers \
  queue_name channel_pid consumer_tag prefetch_count | findstr "{queue_name}"

# 2. Temporarily increase prefetch count for the consumer service
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl set_parameter \
  consumer_prefetch his-hope "{queue_name}" "{\"prefetch_count\": 100}"

# 3. Scale the consumer service to handle the backlog
kubectl scale deploy/his-hope-appointment-service -n his-hope --replicas=5

# 4. If the queue is critical but consumer cannot catch up, purge non-critical messages
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl purge_queue his-hope.{queue_name}
# ⚠ CAUTION: Purge only after confirming no critical events are in the queue
```

### B. Connection Exhaustion

```bash
# 1. Identify which services/apps have the most connections
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_connections \
  name user host peer_host state channels | sort -t$'\t' -k2

# 2. Close connections from unhealthy or zombie consumers
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl close_connection \
  "{connection_name}" "Killed by SRE — connection leak detected"

# 3. Force-close all connections from a specific misbehaving service
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_connections \
  name pid user | findstr "patient-service" | ForEach-Object { \
    $pid = $_ -split '\s+' | Select-Object -Index 1; \
    rabbitmqctl close_connection $pid "SRE forced close" \
  }

# 4. Reduce connection pool in the affected service's app config
# IConnectionFactory: RequestedChannelMax=50, RequestedHeartbeat=30
```

### C. Memory High Watermark / Blocked Publishers

```bash
# 1. Check current memory usage
kubectl exec his-hope-rabbitmq-0 -n his-hope -- \
  rabbitmqctl eval 'vm_memory_monitor:get_total_memory_usage().'

# 2. Find memory-heavy queues
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_queues \
  name messages memory | sort -t$'\t' -k3 -n -r | Select-Object -First 10

# 3. Temporarily block specific publishers by queue limit
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl set_policy \
  max-length "his-hope\.audit\..*" '{"max-length":10000,"overflow":"reject-publish"}' --priority 100

# 4. Increase memory watermark temporarily (last resort)
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl set_vm_memory_high_watermark 0.8
# Default is 0.4 — do not exceed 0.8 without cluster capacity review
```

### D. Node Down — Failover to Replica

```bash
# 1. Check cluster status
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl cluster_status

# 2. Promote a replica if the node is a primary in a mirrored or quorum queue
kubectl exec his-hope-rabbitmq-1 -n his-hope -- rabbitmqctl await_online_peers 3
# Quorum queues auto-heal; mirrored queues may require manual promote

# 3. Restart the failed node
kubectl delete pod his-hope-rabbitmq-1 -n his-hope
```

## Resolution

### Root Cause Fix

| Cause | Fix |
|---|---|
| Slow consumer (blocking DB/API call) | Add async processing with proper timeout; use `Task.Run` only for CPU-bound work |
| Connection leak (`IConnection` not disposed) | Use `IHostedService` singleton connection per service, not per-scope |
| Disk full (queue messages on disk) | Set queue TTL/TTL-based policies; add disk space alert at 70% |
| Queue growth > consumer capacity | Add auto-scaling for consumers based on `rabbitmq_queue_messages_ready` |
| Network partition | Check Cilium policies; ensure RabbitMQ ports (4369, 5672, 25672) are open between nodes |

### Verification

```bash
# 1. All queues have messages_ready < 100
kubectl exec his-hope-rabbitmq-0 -n his-hope -- \
  rabbitmqctl list_queues name messages_ready | findstr -v "^\.\.\.$" | \
  ForEach-Object { if ([int]($_ -split '\s+')[1] -gt 100) { Write-Warning "Queue $_ still over 100" } }

# 2. No memory or disk alarms
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl eval 'alarm:get_alarms().'
# → Should return []

# 3. All consumers active
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_consumers

# 4. Application-level event flow test
kubectl exec deploy/his-hope-clinical-service -n his-hope -- \
  curl -sf -X POST http://localhost:5000/api/v1/test/publish-event \
    -H 'Content-Type: application/json' \
    -d '{"eventType":"ClinicalOrderCreated","patientId":"test-001"}'

# 5. Outbox processor catches up
kubectl logs deploy/his-hope-billing-service -n his-hope --tail=20 | findstr "Outbox"
```

## Postmortem

Use the standard incident postmortem template at `docs/postmortem-template.md`.

Key metrics to capture:
- Queue depth graph for affected queues (last 12h)
- Consumer lag (messages_ready vs messages_unacknowledged)
- Connection count per service over time
- Memory high-watermark threshold and usage at peak
- Whether publisher confirms were enabled (required for production)

---

> **Last updated**: 2026-07-17 | **Maintainer**: @sre | **Next review**: 2026-09-17
