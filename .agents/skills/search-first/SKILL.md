---
name: search-first
description: Research-before-coding workflow for His.Hope. Search for existing tools, libraries, and patterns before writing custom code. Prevents NIH syndrome.
metadata:
  origin: ECC (imported)
---

# Search-First — Research Before You Code

Systematizes the "search for existing solutions before implementing" workflow for His.Hope development.

## Trigger

Use this skill when:
- Starting a new feature that likely has existing solutions
- Adding a dependency or integration
- The user asks "add X functionality" and you're about to write code
- Before creating a new utility, helper, or abstraction
- Evaluating a NuGet/npm package for His.Hope

## Workflow

```
┌─────────────────────────────────────────────┐
│  0. TOOL AVAILABILITY PREFLIGHT             │
│     Check search channels before relying on │
│     them; report skipped channels honestly   │
├─────────────────────────────────────────────┤
│  1. NEED ANALYSIS                           │
│     Define what functionality is needed      │
│     Identify language/framework constraints  │
├─────────────────────────────────────────────┤
│  2. PARALLEL SEARCH                         │
│     ┌──────────┐ ┌──────────┐ ┌──────────┐  │
│     │  NuGet / │ │  MCP /   │ │  GitHub / │  │
│     │  npm     │ │  Skills  │ │  Web      │  │
│     └──────────┘ └──────────┘ └──────────┘  │
├─────────────────────────────────────────────┤
│  3. EVALUATE                                │
│     Score candidates (functionality, maint, │
│     community, docs, license, deps)         │
├─────────────────────────────────────────────┤
│  4. DECIDE                                  │
│     ┌─────────┐  ┌──────────┐  ┌─────────┐  │
│     │  Adopt  │  │  Extend  │  │  Build   │  │
│     │ as-is   │  │  /Wrap   │  │  Custom  │  │
│     └─────────┘  └──────────┘  └─────────┘  │
├─────────────────────────────────────────────┤
│  5. IMPLEMENT                               │
│     Install package / Configure MCP /       │
│     Write minimal custom code               │
└─────────────────────────────────────────────┘
```

## Decision Matrix

| Signal | Action |
|--------|--------|
| Exact match, well-maintained, MIT/Apache | **Adopt** — install and use directly |
| Partial match, good foundation | **Extend** — install + write thin wrapper |
| Multiple weak matches | **Compose** — combine 2-3 small packages |
| Nothing suitable found | **Build** — write custom, but informed by research |

## How to Use

### Step 0: Tool Availability Preflight

Check only the channels relevant to the task at hand.

| Channel | Check | If missing |
|---------|-------|------------|
| Repository search | `rg --files` and targeted `rg` queries | State that only visible files were inspected |
| NuGet | `dotnet nuget list source` | Use web search |
| npm | `npm --version` | Use web search |
| MCP tools | Available tool list | Fall back to official docs |
| Skills directory | `ls .agents/skills/` | Say no skill found |
| GitHub | `gh auth status` | Use public web search |

### Quick Mode (inline)

Before writing code, mentally run through:

0. Does this already exist in the repo? → `rg` through relevant modules first
1. Is this a common .NET pattern? → Search NuGet
2. Is this a common Angular pattern? → Search npm
3. Is there an MCP for this? → Check `opencode.json` MCP section
4. Is there a skill for this? → Check `.agents/skills/`
5. Is there an existing GitHub implementation? → Search

### Full Mode (agent)

For non-trivial decisions, launch a researcher agent:

```
task(explore, "
  Research existing tools for: [DESCRIPTION]
  Platform: .NET 8 / Angular 17
  Constraints: [ANY]

  Search: NuGet, npm, MCP servers, His.Hope skills, GitHub
  Return: Structured comparison with recommendation
")
```

## Search Shortcuts by Category

### .NET Backend
- CQRS → `MediatR`, `Brighter`
- Validation → `FluentValidation`
- Mapping → `Mapster`, `AutoMapper`
- Background jobs → `Hangfire`, `Quartz.NET`
- Resiliency → `Polly`
- gRPC → `protobuf-net.Grpc`, `Grpc.AspNetCore`
- Auth → `Microsoft.AspNetCore.Identity`

### Angular Frontend
- State management → `NgRx`, `Akita`, `NgXs`
- UI components → `Angular Material`
- Forms → `ReactiveFormsModule` (built-in)
- i18n → `@angular/localize`
- Testing → `Jasmine`, `Karma`, `Playwright`

### Data & APIs
- HTTP → `HttpClient` (built-in .NET)
- Validation → `FluentValidation`, `Zod` (Angular)
- Database → EF Core (configured in His.Hope)

## Integration Points

### With @dispatcher
The dispatcher should use search-first before routing:
- If a solution already exists → route to existing agent
- If no existing solution → route to full pipeline

### With @architect
The architect should consult search-first for:
- Technology stack decisions
- Integration pattern discovery
- Existing reference architectures

## His.Hope-Specific Guidance

### Package Evaluation Criteria
- **License**: MIT/Apache preferred. No AGPL for production use.
- **Maintenance**: Updated within last 6 months. Active issue resolution.
- **Dependencies**: Minimal. Avoid dependency hell in His.Hope's microservice ecosystem.
- **Security**: No known CVEs. HIPAA-compatible.
- **.NET 8 compatible**: Must target net8.0 or higher.

### Pre-Approved Categories
- Logging: `Serilog` (already in use)
- ORM: `EF Core` (already in use)
- Validation: `FluentValidation` (already in use)
- CQRS: `MediatR` (already in use)
- Testing: `xUnit`, `FluentAssertions`, `Testcontainers` (already in use)
- UI: `Angular Material` (already in use)

## Anti-Patterns

- **Jumping to code**: Writing a utility without checking if one exists
- **Ignoring MCP**: Not checking if an MCP server already provides the capability
- **Silent skipping**: Reporting "nothing found" when a search channel was unavailable
- **Over-customizing**: Wrapping a library so heavily it loses its benefits
- **Dependency bloat**: Installing a massive package for one small feature
- **Outdated version**: Installing an unmaintained package because it was the first search result
