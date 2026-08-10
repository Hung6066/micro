# Agent Model Upgrade: deepseek-v4-flash → openai/gpt-5.4-mini

- **Status**: Approved
- **Date**: 2026-07-19
- **Deciders**: @architect, product owner

## Context

All 18 flash-tier agents in the His.Hope agent system currently use `opencode-go/deepseek-v4-flash` as their primary language model, with `openai/gpt-5.4-mini` as fallback. The decision was made to promote `gpt-5.4-mini` to primary and eliminate the redundant fallback.

## Scope

### Files to modify

1. **`opencode.json`** — 18 agent entries: change `model` field + remove `fallback_models`
2. **`.opencode/agents/*.md`** — 15 agent frontmatter files: change `model:` field

### Agents affected (flash → gpt-5.4-mini)

testing-backend, testing-frontend, validate, check-ui, dotnet, angular, devops, dba, security, ml-ai, data-platform, qa, dispatcher, orchestrator, docs, explore, git, e2e-test

### Agents NOT affected (remain pro)

architect, plan, harness-runner, loop-engineer

## Decision

- Primary model: `"openai/gpt-5.4-mini"` (was `"opencode-go/deepseek-v4-flash"`)
- Fallback: removed entirely (was `["openai/gpt-5.4-mini"]`)
- Pro agents: no change

## Consequences

### Positive
- Single model to maintain for flash-tier agents
- Eliminates redundant fallback that matched the new primary
- Simple, zero-risk config change (no code/logic touched)

### Negative
- Any behavioral differences between deepseek-v4-flash and gpt-5.4-mini will surface immediately (no fallback cushion)
- git history will show 33+ file changes for a config-only update

## Rollback

```bash
git revert <commit-hash>
```
