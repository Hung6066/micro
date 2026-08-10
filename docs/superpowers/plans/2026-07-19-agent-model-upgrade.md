# Agent Model Upgrade: deepseek-v4-flash → openai/gpt-5.4-mini

> **For agentic workers:** Simple config-only change. Execute tasks inline.

**Goal:** Replace primary model `opencode-go/deepseek-v4-flash` with `openai/gpt-5.4-mini` for all 18 flash-tier agents, removing redundant fallback.

**Architecture:** Pure configuration change across `opencode.json` + 15 agent markdown frontmatter files. No code/logic changes.

**Tech Stack:** JSON config, YAML frontmatter, git

## Global Constraints

- Only modify `model` and `fallback_models` fields — never change agent descriptions, permissions, MCP configs, or tools
- Only touch flash agents (model containing `deepseek-v4-flash`), never pro agents
- Pro agents (architect, plan, harness-runner, loop-engineer) remain untouched
- All changes must be surgical — no whitespace/formatting adjustments

---

### Task 1: Update `opencode.json` — 18 agent model entries

**Files:**
- Modify: `opencode.json` (lines 40-160, 18 agent entries)

- [ ] **Step 1: Change `model` + remove `fallback_models` for each agent**

For each of these 18 agents, change `"model": "opencode-go/deepseek-v4-flash"` to `"model": "openai/gpt-5.4-mini"` and delete the `"fallback_models": ["openai/gpt-5.4-mini"]` line:

testing-backend, testing-frontend, validate, check-ui, dotnet, angular, devops, dba, security, ml-ai, data-platform, qa, dispatcher, orchestrator, docs, explore, git, e2e-test

- [ ] **Step 2: Verify opencode.json is valid JSON**

Run: `python -c "import json; json.load(open('opencode.json', encoding='utf-8')); print('Valid JSON')"` or equivalent check

### Task 2: Update `.opencode/agents/*.md` — 15 agent frontmatter files

**Files:**
- Modify: 15 `.md` files under `.opencode/agents/`

- [ ] **Step 1: Change `model:` frontmatter field**

For each of these 15 files, change `model: opencode-go/deepseek-v4-flash` to `model: openai/gpt-5.4-mini`:

angular-agent.md, check-ui-agent.md, data-platform-agent.md, dba-agent.md, devops-agent.md, docs-agent.md, dotnet-agent.md, e2e-test.md, git-agent.md, ml-ai-agent.md, qa-agent.md, security-agent.md, testing-backend-agent.md, testing-frontend-agent.md, validate-agent.md

- [ ] **Step 2: Verify no `deepseek-v4-flash` remains in agent files**

Run: `rg "deepseek-v4-flash" .opencode/agents/` — should return 0 matches

### Task 3: Verify and commit

- [ ] **Step 1: Verify no remaining `deepseek-v4-flash` across repo**

Run: `rg "deepseek-v4-flash" opencode.json .opencode/agents/` — must return 0 matches

- [ ] **Step 2: Stage changed files**

```bash
git add opencode.json .opencode/agents/*.md
git add docs/superpowers/specs/2026-07-19-agent-model-upgrade-design.md
git add docs/superpowers/plans/2026-07-19-agent-model-upgrade.md
```

- [ ] **Step 3: Commit with Conventional Commits**

```bash
git commit -m "chore(config): update 18 flash agents from deepseek-v4-flash to openai/gpt-5.4-mini"
```
