# ADR 005: RabbitMQ with Outbox Pattern for Async Events

**Status**: Accepted

**Date**: 2026-07-16

**Context**: Need reliable event-driven communication for cross-service workflows (e.g., appointment created -> billing notification). Must not lose events on crash.

**Decision**: RabbitMQ as message broker with transactional Outbox pattern (events written to DB in same transaction as domain changes, then published by background processor).

**Consequences**: Additional infrastructure (RabbitMQ). Outbox adds latency (~50-100ms). At-least-once delivery requires idempotent handlers.
