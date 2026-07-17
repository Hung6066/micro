### Task 5: Update opencode.json

**Files:**
- Modify: `opencode.json`

**Interfaces:**
- Modifies: references object (add `knowledge`), agents.docs.description, agents.validate.description

**Global constraints:**
- JSON must remain valid after all edits
- Must preserve existing formatting (2-space indent)

- [ ] **Step 1: Read current opencode.json to confirm structure**

Read `opencode.json` — confirm the exact text of the references section end, and the @docs/@validate descriptions.

- [ ] **Step 2: Add knowledge reference**

Add after the `"cockroach"` reference (last item in the references object, before the closing `}`):

Current last reference:
```json
    "cockroach": {
      "path": "cockroach",
      "description": "Use for CockroachDB migrations and configs"
    }
```

Add a comma after the cockroach closing `}` and insert:
```json
    "knowledge": {
      "path": "docs/knowledge",
      "description": "Use for agent knowledge base: gotchas, patterns, decisions"
    }
```

- [ ] **Step 3: Update @docs agent description**

Find the `"docs"` agent object in the `"agent"` section. Change its `"description"` field.

From:
```
"description": "Documentation agent — ADRs, API docs, service READMEs, changelogs, runbooks, dev guides. Runs in Phase 2 (generate) + Phase 4 (verify) of pipeline. Uses MCP: filesystem for reading/writing docs, github for git operations on doc files.",
```

To:
```
"description": "Documentation agent — ADRs, API docs, service READMEs, changelogs, runbooks, dev guides, knowledge base (capture & verify). Runs in Phase 2 (generate) + Phase 4 (verify) of pipeline. Uses MCP: filesystem for reading/writing docs, github for git operations on doc files.",
```

- [ ] **Step 4: Update @validate agent description**

Find the `"validate"` agent object. Change its `"description"` field.

From:
```
"description": "Validation agent (API contract, schema, FluentValidation, build, config/secrets, migration safety). Uses MCP: db-* for migration verification, docker for container validation, filesystem for config inspection.",
```

To:
```
"description": "Validation agent (API contract, schema, FluentValidation, build, config/secrets, migration safety, knowledge index freshness). Uses MCP: db-* for migration verification, docker for container validation, filesystem for config inspection.",
```

- [ ] **Step 5: Validate JSON syntax**

```powershell
Get-Content opencode.json | ConvertFrom-Json | Out-Null
Write-Host "JSON valid"
```

Expected: "JSON valid" — no parse errors

- [ ] **Step 6: Commit**

```bash
git add opencode.json
git commit -m "feat(knowledge): integrate knowledge base into opencode.json references and agent descriptions"
```
