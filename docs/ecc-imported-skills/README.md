# ECC-Imported Skills

These skills were imported from [affaan-m/ECC](https://github.com/affaan-m/ECC) (MIT license) and adapted for His.Hope.

## Imported Skills

| Skill | ECC Source | Adaptation |
|---|---|---|
| `verification-loop` | `skills/verification-loop/` | Added .NET/Angular-specific checks, HIPAA/PII scanning |
| `eval-harness` | `skills/eval-harness/` | Added His.Hope domain evals (Patient, Clinical, Billing) |
| `autonomous-loops` | `skills/autonomous-loops/` | Adapted to use @dispatcher/@orchestrator pipeline patterns |
| `search-first` | `skills/search-first/` | Added NuGet search, pre-approved package list |
| `security-review` | `skills/security-review/` | Rewritten for HIPAA compliance, Vault integration, .NET patterns |
| `continuous-learning-v2` | `skills/continuous-learning-v2/` | Custom implementation using .NET harness MemoryService + 2 new MCP tools |

## Installation

Skills are auto-discovered from `.agents/skills/` by OpenCode:

```bash
# Each skill is a subdirectory with SKILL.md:
# .agents/skills/<skill-name>/SKILL.md
```

To install (if starting from scratch):
```bash
# Copy from this docs directory to the runtime skills directory
mkdir -p .agents/skills/verification-loop
cp docs/ecc-imported-skills/verification-loop.md .agents/skills/verification-loop/SKILL.md
# ... repeat for each skill
```

## MCP Tools

The `continuous-learning-v2` skill requires two MCP tools exposed by the .NET Agent Harness:

| Tool | Endpoint | Description |
|---|---|---|
| `record-instinct` | `agent-harness` MCP | Save a learned pattern after a fix |
| `query-instincts` | `agent-harness` MCP | Search past instincts by error pattern |

These are registered in `src/AgentHarness.Mcp/Tools/` and exposed automatically.

## Upstream

- ECC Repository: https://github.com/affaan-m/ECC
- License: MIT
- Version imported: v2.0.0
- Original skill sources: `skills/<name>/SKILL.md` in ECC repo
