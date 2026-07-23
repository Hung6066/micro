### Task 5: Service Dependency Graph

**Frontend only (no backend changes — data from existing resource API):**
- Create `src/app/features/resources/dependency-graph.component.ts` — interactive SVG/Canvas graph
- Add tab or toggle in resources page to switch between card view and graph view
- Graph nodes: services (colored by health), databases, infra
- Graph edges: derived from `databases[]` field on ServiceResource + known dependencies

---

