# ADR 001: Microservices Architecture with Clean Architecture

**Status**: Accepted

**Date**: 2026-07-16

**Context**: Hospital system needs independent deployability, HIPAA isolation, scalable domain logic. Monolith would create coupling between billing, clinical, pharmacy.

**Decision**: 7 microservices with per-service Clean Architecture (4 layers: Api, Application, Domain, Infrastructure). Each service owns its data.

**Consequences**: Network overhead, distributed transaction complexity (solved by Outbox + Saga), operational complexity.
