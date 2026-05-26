# HSM Phase 7: Claude Tooling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the HSM framework Claude-first: comprehensive CLAUDE.md, scaffolding skills for all HSM components, an MCP server for HSM tree introspection, and guidelines for consumer projects.

**Architecture:** Skills are `.claude/skills/` directories with `SKILL.md` files. The MCP server is a Node.js TypeScript server using `@modelcontextprotocol/sdk` that parses C# source files via regex to build the state tree. Guidelines are a markdown document.

**Tech Stack:** Markdown (skills), Node.js/TypeScript (MCP server), C# (target codebase)

---

Root: `/Users/vladislavpanin/Documents/Docs/Aspid/Packages/Aspid.Core.HSM/Projects/Aspid.Core.HSM-phase7/`

---

### Task 1: Update CLAUDE.md with full HSM architecture documentation

Update the existing `CLAUDE.md` to reflect all 6 phases of the architecture. This is the most impactful change — Claude reads this file on every conversation.

Sections to add/update:
- Complete type hierarchy (Controller → ControllerGroup → State)
- Transition pipeline (ITransition, guards, chain fallback, TransitionTo/TransitionVia)
- Extension States (IExtensionState, attach/detach, auto-detach)
- Async support (AsyncMode Sequential/Parallel, AsyncOf pairing)
- Scope management (IStateScope, ScopeLifetime Transient/Cached)
- Extension points (IsControllerEnabled, IsStateEnabled)
- Compile-time diagnostics (HSM001)
- File layout of all runtime and generator files
- Common patterns: "how to add a state", "how to add a controller", etc.

Commit: `docs: update CLAUDE.md with full HSM architecture reference`

---

### Task 2: Scaffolding skills — create-state, create-controller, create-transition, create-extension

Create 4 skills in `.claude/skills/`:

**create-state/SKILL.md** — Creates a new HSM state:
- Prompts for: state name, parent state (optional), controllers
- Generates: partial class with [ControllerGroup], [ParentState], AddControllers()
- Registers in StateFactory

**create-controller/SKILL.md** — Creates a new controller:
- Prompts for: controller name, interfaces (IUpdateController, IEnterController, etc.)
- Generates: class implementing selected interfaces

**create-transition/SKILL.md** — Creates a new transition:
- Prompts for: source state, target state, guard logic
- Generates: class with [Transition], ITransition, CanTransition()
- Shows where to register in StateMachine

**create-extension/SKILL.md** — Creates a new extension state:
- Prompts for: extension name, compatible states, controllers
- Generates: class with [ExtensionFor], [ControllerGroup], IExtensionState

Commit: `feat: add scaffolding skills for HSM components`

---

### Task 3: MCP server — hsm-analyzer

Create a Node.js MCP server that analyzes the C# codebase and exposes HSM structure.

**Location:** `.claude/mcp-servers/hsm-analyzer/`

**Tools exposed:**
1. `hsm_state_tree` — Returns the state hierarchy (parsed from [ParentState] attributes)
2. `hsm_transitions` — Lists all registered transitions (parsed from [Transition] attributes)
3. `hsm_extensions` — Lists extension states and their compatible hosts (parsed from [ExtensionFor])
4. `hsm_controllers` — Lists all controllers and which interfaces they implement

**Implementation:** Parse .cs files via regex for attributes, build tree structure, return as JSON.

**Registration:** Add to `.mcp.json` alongside existing context7 and github servers.

Commit: `feat: add hsm-analyzer MCP server for codebase introspection`

---

### Task 4: Consumer project guidelines

Create `docs/guidelines/consumer-project-setup.md`:

- How to organize states/controllers/transitions in a consumer project
- XML documentation patterns for controllers (so Claude understands purpose)
- CLAUDE.md template for consumer project feature folders
- File naming conventions
- Example project structure for a game

Commit: `docs: add consumer project guidelines for Claude-first DX`

---

### Task 5: Final verification and commit

- Verify all skills are valid (no syntax errors in SKILL.md frontmatter)
- Verify MCP server starts and responds
- Build the C# project to confirm nothing broke
