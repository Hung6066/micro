# His.Hope — Karpathy Coding Guidelines

These guidelines reduce common LLM coding mistakes. They supplement the project's architecture and routing instructions above.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

**Project-specific:** The PermissionGuard `take(1)` bug, backend logout 500, and CockroachDB global tables are known gotchas. Check `docs/knowledge/` before repeating past mistakes.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: *"Would a senior engineer say this is overcomplicated?"* If yes, simplify.

**Project-specific:** His.Hope uses Clean Architecture — don't add layers that aren't needed. A simple query handler doesn't need a separate repository interface + implementation if EF Core is the only data access.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it — don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: *Every changed line should trace directly to the user's request.*

**Project-specific:** Angular components often have standalone flags, inline templates, and Material Design patterns. Don't convert inline templates to separate files or add/remove standalone unless asked.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

**Project-specific:** E2E tests are in `tests/e2e/`. Backend tests are alongside each service's `tests/` folder. Always run `npx playwright test --workers=1` before claiming a change is complete.

## Project Context Integration

| Principle | Applies to His.Hope as... |
|-----------|--------------------------|
| Think Before Coding | Review known gotchas in `docs/knowledge/` before starting |
| Simplicity First | Don't over-engineer microservice boundaries; start coarse, split only if needed |
| Surgical Changes | Match existing patterns (CQRS, MediatR, Clean Architecture layers) |
| Goal-Driven | Define testability criteria first; His.Hope has full E2E + unit test infrastructure |

## Agent Harness & Loop Engineer

The His.Hope agent system is powered by a runtime harness (.NET 8 MCP server at `src/Infrastructure/AgentHarness/`).

### Key Components
- **Agent Harness MCP Server** — Stateful pipeline execution engine with CockroachDB persistence
- **Loop Engineer** (`@loop-engineer`) — Autonomous fix agent that intercepts failed quality gates and applies fixes
- **MCP Tools**: `harness_start_pipeline`, `harness_get_status`, `harness_dispatch_agent`, `harness_cancel_pipeline`

### When to Use
- Use `harness_start_pipeline` to run any agent pipeline with state persistence
- Loop Engineer activates automatically when a quality gate fails
- Loop Engineer will auto-fix if confidence > 0.8, otherwise escalate to human
