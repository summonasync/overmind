# Agent connection guide

How an AI coding agent connects to the Overmind Host on the four runtimes the crew
ships a kit for — **Claude Code**, **Qwen Code**, **Kimi Code CLI**, **OpenAI Codex CLI** —
plus what the app materializes for each and which kit behaviors are live today.

Every path, field and behavior below carries a citation (`file:line` in this repo, or an
audit file for measured runtime behavior). Read [docs/mcp-tool-contracts.md](mcp-tool-contracts.md)
for the per-tool wire contract; read the `overmind-ceremony` skill for the planning ceremony
the tools drive. Install steps for the Host itself: `docs/install-windows.md`,
`docs/install-ubuntu.md`.

**Live vs dark.** Two markers are used, and they are load-bearing:

- **live on main** — the behavior runs in the shipped Host today.
- **dark until 251** — the behavior belongs to the *global (HOME) kit materialization*
  card 251 and does **not** exist on main. As measured on 2026-11-20 (+03), 251 is
  `inProgress` with only its first task running — see "Six kits, live vs dark".

---

## 1. Topology: one Host, one thin adapter

```
AI agent (claude / qwen / kimi / codex)          Browser
      | stdio JSON-RPC                               |
      v                                              v
Overmind.Mcp (console adapter)  ----HTTP---->  Overmind.Host (single process)
                                               ├─ Kestrel minimal API, 127.0.0.1 only
                                               ├─ static SPA hosting (web/out)
                                               └─ Avalonia TrayIcon (no native windows)
                                                       |
                                                       v
                                          Overmind.Infrastructure
                                          ├─ SQLite (WAL) + FTS5  — always on
                                          └─ Qdrant (Docker)      — optional, vector search
```

The diagram and its rules are `CONTEXT.md:11-27`; the dependency direction
(`Core ← Infrastructure ← Host`, `Overmind.Mcp` references nothing) is `STRUCTURE.md:37`.

What an agent actually needs to know from it:

- **The adapter is a separate executable.** `src/Overmind.Mcp` is a console app, assembly
  name `overmind-mcp`, a thin HTTP→JSON-RPC proxy that references no other project and
  reads Host DTOs through tolerant `JsonElement` deserialization (`CONTEXT.md:72`).
  Because the reads are tolerant, the Host↔MCP contract is **additive-only** — a renamed or
  removed DTO field breaks the adapter silently rather than at compile time
  (`STRUCTURE.md:37`, `STRUCTURE.md:45`).
- **Transport is stdio.** The runtime spawns the adapter as an MCP server; the server name
  is `overmind`, and its instructions tell the agent to call `overmind_onboard` first
  (`CONTEXT.md:104`).
- **No agent ever talks to the database.** The HTTP API is the only contract; Kestrel binds
  `127.0.0.1` (default port 4761, free-port fallback, `Overmind:BindUrl` override), there is
  no auth in v1 and loopback *is* the security boundary (`CONTEXT.md:94`).
- **Port discovery.** The Host writes its bound port to `~/.overmind/host.port` at startup.
  Consumers resolve the base URL in this order: `OVERMIND_API_URL` env var → port file →
  default 4761 (`CONTEXT.md:95`). Set `OVERMIND_API_URL` only when you run several Hosts
  (a lane build, a test instance) and need the adapter pinned to one of them.
- **Content roots are pinned, so cwd does not matter.** Both Host and adapter pin their
  content root to the install dir, not the caller's cwd — the adapter can be spawned from a
  huge repo tree at near-zero inotify cost (`CONTEXT.md:26`).
- **Failure mode you will see.** The adapter exits with code 2 and a stderr hint if the Host
  API is unreachable at startup (`CONTEXT.md:72`). If a runtime reports the MCP server as
  failed to start, start the Host (or click the tray icon → Open) before debugging the config.
- **Two HTTP doors.** Human-facing routes live under `/api/*`, agent-facing ones under
  `/api/agent/*`; the distinction a gate depends on is carried by the route prefix, never by
  a header or body parameter (`STRUCTURE.md:46`). The MCP adapter calls the agent door.

## 2. Registration: `overmind-mcp install`

Registration is self-service and file-based — the installer never shells out to agent CLIs.
`overmind-mcp install` (`CONTEXT.md:74`) upserts only the `overmind` key, leaves unknown keys
and tables alone, takes a timestamped `.bak` before its first mutation, converges to
`unchanged` on re-run, and is intercepted **before** the Host health check, so it works while
the Host is down. Flags: `--all` (default = detected agents only), `--agents <csv>`,
`--project` (git-root scope instead of user-global), `--remove`, `--dry-run`. Exit codes:
0 ok / 1 any failure / 2 usage.

**Detection** (`--all`, the default set): an agent counts as detected when a config-dir
candidate exists **or** its binary is on `PATH` — `AgentDetector.IsDetected`,
`src/Overmind.Mcp/Registration/AgentDetector.cs:7-11`, candidates and binary names per agent
in `src/Overmind.Mcp/Registration/AgentRegistry.cs:8-70`.

| Runtime | User-global MCP config | Project-scope MCP config | Format | Registry row |
|---|---|---|---|---|
| claude | `~/.claude.json` (top-level `mcpServers`) | `<gitroot>/.mcp.json` | JSON `mcpServers` | `AgentRegistry.cs:21-30`, `CONTEXT.md:78` |
| qwen | `~/.qwen/settings.json` | `<gitroot>/.qwen/settings.json` | JSON `mcpServers` | `AgentRegistry.cs:53-62`, `CONTEXT.md:80` |
| kimi-code | `$KIMI_CODE_HOME/mcp.json` (fallback `~/.kimi-code/mcp.json`) | `<gitroot>/.mcp.json` | JSON `mcpServers` | `AgentRegistry.cs:8-19`, `CONTEXT.md:77` |
| codex | `$CODEX_HOME/config.toml` (fallback `~/.codex/config.toml`) | `<gitroot>/.codex/config.toml` | TOML `[mcp_servers.*]` (Tomlyn 0.20.x, BSD-2-Clause) | `AgentRegistry.cs:32-41`, `CONTEXT.md:79` |

**Dev-loop caveat** (`CONTEXT.md:83`): running the installer through `dotnet exec
overmind-mcp.dll` makes `Environment.ProcessPath` the muxer, so a non-dry-run install from a
dev loop registers the *wrong* command path. Install from the packaged binary, or from the
stable link `~/apps/overmind/mcp/overmind-mcp`, which the registered path prefers when it
resolves to the running binary (`CONTEXT.md:74`).

**The onboard block.** `install --project` also writes a managed block into the repo's agent
instruction file (`CLAUDE.md` / `QWEN.md` / `AGENTS.md`, first existing candidate wins —
`src/Overmind.Mcp/Registration/AgentInstructionFile.cs:6-26`, call site
`InstallCommand.cs:223`), delimited by `<!-- overmind:begin -->` / `<!-- overmind:end -->`
(`src/Overmind.Mcp/Registration/OnboardManagedBlock.cs:5-6`). The block states the project
name and id, tells the agent to call `overmind_onboard` first, declares the
Overmind-vs-Grimoire domain boundary, and names `OVERMIND_PROJECT_ID` as the escape hatch for
symlinked or worktree layouts (`src/Overmind.Mcp/Registration/OnboardBlockContent.cs:13-22`).
If the Host is unreachable at that moment the block is written with placeholders and the
installer warns you to re-run it once the Host is up (`InstallCommand.cs:201-210`).

## 3. Claude Code

- **MCP registration:** `~/.claude.json`, top-level `mcpServers` key (user-global); per-repo
  `<gitroot>/.mcp.json`. Detection candidates `~/.claude` and `~/.claude.json`, binary `claude`
  (`AgentRegistry.cs:21-30`).
- **Instruction files:** `CLAUDE.md` at the repo root; the kit side lives in `.claude/`
  (`src/OvermindCrewKit/SYNC-STRUCTURE.md:5`).
- **Kit home paths (deployed by the crew install script):** `~/.claude/agents`,
  `~/.claude/skills`. `~/.claude/skills` is **merge** mode since 2026-10-15 because the home
  also hosts Anthropic-synced skills that a whole-directory mirror would delete
  (`SYNC-STRUCTURE.md:258`).
- **Project materialization target:** `.claude/skills` (`platforms.json:2`, key `claude`).
- **Enforcement posture:** prompt-enforced only — Claude has no deny/ask layer the kit uses,
  so behavioral rules live in `CLAUDE.md` and the skill bodies
  (`SYNC-STRUCTURE.md:172`, the "Deterministic enforcement" row).

## 4. Qwen Code

- **MCP registration:** `~/.qwen/settings.json` under `mcpServers` (user-global); per-repo
  `<gitroot>/.qwen/settings.json`. Detection candidate `~/.qwen`, binary `qwen`
  (`AgentRegistry.cs:53-62`).
- **Instruction file:** `QWEN.md` at the repo root; kit side lives in `.qwen/`
  (`SYNC-STRUCTURE.md:5`).
- **Kit home paths:** `~/.qwen/agents`, `~/.qwen/skills`, `~/.qwen/hooks` — all **merge**
  mode, because `~/.qwen/` hosts non-crew skills (third-party content survives; a crew skill
  deleted from the repo leaves an orphan until removed by hand — the documented `merge`
  limitation, `SYNC-STRUCTURE.md:254-256`).
- **Project materialization target:** `.qwen/skills` (`platforms.json:7`).
- **Runtime config the kit does NOT own:** `~/.qwen/settings.json` `permissions` + `hooks`
  ship as a *reference* copy, `.qwen/settings-snippet.jsonc`, never installed over the live
  file — merging is a human action (`SYNC-STRUCTURE.md:161`). Model pins for sub-agents
  resolve through `modelProviders.openai` in the same file (`SYNC-STRUCTURE.md:170`).

## 5. Kimi Code CLI

- **MCP registration:** `$KIMI_CODE_HOME/mcp.json` with fallback `~/.kimi-code/mcp.json`
  (user-global); per-repo `<gitroot>/.mcp.json`. Detection candidates `$KIMI_CODE_HOME` and
  `~/.kimi-code`, binary `kimi` (`AgentRegistry.cs:8-19`).
- **Instruction file:** `AGENTS.md` at the repo root; kit side lives in `.kimi-code/`
  (`SYNC-STRUCTURE.md:5`).
- **Kit home paths:** `~/.kimi-code/agents`, `~/.kimi-code/skills` (mirror mode), plus
  `AGENTS.md`, `SKILLFLOW.md`, `SOCIAL_DICTIONARY.md`, `WAKE-CHANNEL.md` and the Kimi-only
  `config.toml` (secondary-model pool) (`src/OvermindCrewKit/manifest.json`, entries with
  `home: kimi`; `SYNC-STRUCTURE.md:157-163`).
- **Project materialization target:** `.kimi-code/skills` (`platforms.json:3`, key
  `kimiCode` — the platforms.json key spelling differs from the registry name `kimi-code`;
  both are current, the loader reads `platforms.json` through `PlatformDirectoryMap`,
  `CONTEXT.md:50-51`).
- **Shared generic-skills home:** Kimi also reads `~/.agents/skills/` (under the real OS home,
  not `KIMI_CODE_HOME`) and merges it with its own skills — the reason the shared `.agents`
  directory is a duplication, not a collision (`audits/review-250-t5-codex-evidence-2026-11-19.md:97-99`).

## 6. Codex

- **MCP registration:** `$CODEX_HOME/config.toml` with fallback `~/.codex/config.toml`,
  written as TOML `[mcp_servers.*]`; per-repo `<gitroot>/.codex/config.toml`. Detection
  candidates `$CODEX_HOME` and `~/.codex`, binary `codex` (`AgentRegistry.cs:32-41`).
- **Instruction file:** `AGENTS.md` at the repo root; kit side lives in `.codex/`
  (`SYNC-STRUCTURE.md:5`).
- **Kit home paths:** `~/.codex/agents` and `~/.codex/skills`, both **mirror** mode by user
  decision (2026-10-07): those directories are crew-owned, so third-party roles or skills
  dropped there are deleted on the next install (`SYNC-STRUCTURE.md:260`).
- **Project materialization target:** `.agents/skills` (`platforms.json:4`). Codex scans repo
  `.agents/skills` CWD→repo root, plus `$HOME/.agents/skills`, admin `/etc/codex/skills` and
  its own bundled set; shared names are **not** merged — both appear in the selectors
  (`SYNC-STRUCTURE.md:262`, `audits/review-250-t5-codex-evidence-2026-11-19.md:92-95`).
- **The home deploy target is still the legacy `~/.codex/skills` — not `.agents/skills`.**
  Measured on codex-cli 0.160.1: `$CODEX_HOME/skills` is scanned as a skill root even though
  current Codex docs no longer name it, so the legacy home keeps working on the installed
  CLI (`audits/review-250-t5-codex-evidence-2026-11-19.md:65-69`; decision write-up
  `SYNC-STRUCTURE.md:262`, surface (3)). Relocating the install target to
  `$HOME/.agents/skills` is an open follow-up tracked as **card 253** — until 253 lands, do
  not advertise `.agents/skills` as the *home* target; it is the *project* materialization
  target only.
- **Enforcement posture:** behavioral/prompt-only by crew decision. Codex has hooks in the
  runtime, but the kit ships none and never writes `~/.codex/config.toml` — model pins live
  in the TOML role files, and the config itself stays human-owned
  (`SYNC-STRUCTURE.md:172`, `SYNC-STRUCTURE.md:160`).

### 6.1 Codex role files: the TOML truth

Agent roles on the Codex side are standalone TOML files (`.codex/agents/*.toml`, 46 of them),
and the deserializer is **strict**. Exactly four fields are legal:

```toml
name = "dotnet-coder"
description = "Write, edit, or refactor C# code following senior .NET developer standards."
model = "gpt-6-luna"
developer_instructions = """…the md-side agent body…"""
```

- Any field beyond those four **drops the role**. The drop is **not silent**: the loader
  prints `warning: Ignoring malformed agent role definition …` on stderr at session start and
  the session continues (fail-soft, observable).
- A `tools` field is worse than an unknown field: the key belongs to Codex's
  **tool-configuration** namespace and is deserialized against `WebSearchToolConfigInput`, so
  it collides and can never express a per-role tool allowlist — not even with a lucky value.
- Evidence (probes on codex-cli 0.160.1, scratch `CODEX_HOME`, live `~/.codex/` untouched):
  `audits/review-250-t5-codex-evidence-2026-11-19.md:11-35` — `w2-ok` (four fields) produced
  no warning as the control; `w2-extra` (`tools = [...]`) and `w2-unknown`
  (`someUnknownField = 42`) were both dropped with the warnings quoted above; the
  namespace-collision finding is `audits/review-250-t5-codex-evidence-2026-11-19.md:32-34`.
- Consequences worth writing into any port: per-role restriction cannot live in the role
  container, so it lives in `.codex/AGENTS.md` and the skill bodies. The 46 role files carry
  zero `mcp__` tokens — re-measured 2026-11-19: `SYNC-STRUCTURE.md:174`, and the kit-side
  denominator in `audits/review-250-t5-codex-evidence-2026-11-19.md:35-36` (0 of 46). That is
  the *absence of a place to put it*, not an oversight.
  The checklist gate that enforces the four-field shape is `SYNC-STRUCTURE.md:306-318`.
- Format note: roles are TOML, so the frontmatter quoting rule for `: ` in markdown
  frontmatter does not apply to them; a one-line TOML `description` needs `"` escaped as `\"`
  instead (`SYNC-STRUCTURE.md:31`).

## 7. Six kits: what is live, what is dark

Membership of the six kits is declared in one file, `src/OvermindCrewKit/kit-membership.json`
(shape and the six fixed keys: `src/OvermindCrewKit/KIT-MEMBERSHIP.md:11-33`; the
classification table with `overmind-ceremony` marked mandatory: `KIT-MEMBERSHIP.md:55-62`).
Counts on every side: 57 skill dirs, 46 agent definitions, 15 prose/config files
(`KIT-MEMBERSHIP.md:70-73`).

| Claim | State on main | Evidence |
|---|---|---|
| Six-kit shape exists (membership map, pack per kit, bundled manifest carrying `{name, mandatory}`) | **live on main** | `KIT-MEMBERSHIP.md:11-33`; `build/pack.sh:189-193` |
| Per-project kit activation for **optional** kits (activate / deactivate / drift, one seam, materializer writes the platform dirs above) | **live on main** since 250 | `KitActivationService.cs:11-39`, `CONTEXT.md:65` (`KitsApi` incl. activate/deactivate/drift) |
| `overmind-ceremony` is flagged mandatory and lands through the bundled seed | **live on main** as a DB flag; the seed reproduces the `mandatory` flag (`BundledSeedService.cs:72`, `SkillKit.cs:16,72`) | `CONTEXT.md:61`, `build/pack.sh:190-193` |
| The mandatory kit reaching an agent **HOME** directory *from the app* | **dark until 251** | see below |
| The mandatory kit being activatable **per project** | **refused by design** — `SkillKit.EnsureProjectActivatable` throws `MandatoryGlobalKit` and `KitActivationService` calls it before any disk work (`KitActivationService.cs:23`, `SkillKit.cs:82-88`) | live guard, live behavior |

**The 251 hole, stated plainly.** On main, the mandatory ceremony kit has **no global door**:
`BundledSeedService.RouteMandatoryKitsAsync` finds `IGlobalKitDeployer` unwired and logs
`"Mandatory kit(s) {KitNames} have no global door wired into the host; mandatory-global
materialization is pending feature 251"` (`src/Overmind.Infrastructure/Kits/BundledSeedService.cs:243-257`;
the seam itself is an empty-by-design interface, `src/Overmind.Infrastructure/Kits/IGlobalKitDeployer.cs`).
The interface has no implementation in `Kits/` on main — checked at the time of writing.
**Dark until 251 lands:** materializing kits into `~/.claude/skills`, `~/.qwen/skills`,
`~/.codex/skills` (or its future `.agents/skills` target) *from the app*, with DB-tracked rows,
per-agent opt-in, drift detection and a UI door. Today the only way skills reach a home is the
kit repo's own `install.sh` / `install.ps1`, driven by
`src/OvermindCrewKit/manifest.json` — outside the app, untracked in the DB, and invisible to
the app's drift surface (`251` feature body, measured 2026-11-18; script semantics
`SYNC-STRUCTURE.md:247-260`).

**Board state at the time of writing (recorded as required by the task contract):** feature
`251-global-kit-materialization-tracked-home-deploy-per-agent-fro` read on **2026-11-20**
through `planning_read_feature`: status `inProgress`, priority `p1`, claim alive on
`mark-wolff` (claude), spec and plan `validated`, task T0 `inProgress` (3/4 AC), tasks
T1–T6 `todo`/`blocked`. Nothing global-deploy has landed on main yet — every sentence above
marked **dark until 251** stays dark until the seed warning at `BundledSeedService.cs:243-257`
is replaced by a wired deployer.

**Skill delivery is not a protocol.** There is no "send me the skill over MCP" path: agents
pick skills up *natively* from the materialized directories above (`CONTEXT.md:7`, "skill-kits
are materialized into project folders so agents pick them up natively"). `get_skill`-style
runtime delivery stays a deferred non-goal of 251 (251 feature body, `<non-goals>`), and the
adapter's `install --project` onboard block is not a skill transport — it is the project
identity marker described in section 2.

## 8. gemini / antigravity

One honest line: `platforms.json` carries `gemini` (`.gemini/skills`) and `antigravity`
(`.antigravity/skills`) as **materialization targets only** — no mirror kit side, by the
owner's scope ruling, which is a scope choice and not a capability gap
(`platforms.json:5-6`, `SYNC-STRUCTURE.md:264`).

## 9. Quick checklist when an agent sees no Overmind tools

1. Is the Host running? `overmind-mcp` exits 2 with a stderr hint if the API is unreachable
   (`CONTEXT.md:72`). Check `~/.overmind/host.port` and, if several Hosts run,
   `OVERMIND_API_URL` (`CONTEXT.md:95`).
2. Did the registration land in the right file for your runtime? See the table in section 2 —
   codex is TOML `[mcp_servers.*]`, the other three are JSON `mcpServers`.
3. Re-run `overmind-mcp install --dry-run` (or `--project --dry-run`) and read the plan; it
   converges to `unchanged` on re-run, so a diff means the file was hand-edited (`CONTEXT.md:74`).
4. Codex only: if a crew role is missing from the roster, check its TOML for a fifth field —
   the loader dropped it and warned on stderr (section 6.1).
5. Skills missing: check the *project* target for your runtime (sections 3-6,
   `platforms.json`) first; the HOME copy is a separate act, and until 251 lands the app does
   not manage HOME at all (section 7).

---

## Evidence index

Sources read for this page, all under the lane worktree at `feat/256-release-docs-pack`
(`5904f671`). Counts are measured, not hand-tallied: a regex pass over the body of this page
(sections 1-9, the index itself excluded) counting `file:line` citation occurrences per source
file. Method and denominators are printed in the task log on the board.

| Source | Citation occurrences |
|---|---|
| `CONTEXT.md` (topology, adapter, install table, comm rules) | 21 |
| `src/OvermindCrewKit/SYNC-STRUCTURE.md` (codex semantics, deploy modes, enforcement rows) | 20 |
| `src/Overmind.Mcp/Registration/AgentRegistry.cs` | 9 |
| `audits/review-250-t5-codex-evidence-2026-11-19.md` (W2, AC4, bonus, shared-root legs) | 6 |
| `src/Overmind.Infrastructure/Materialization/platforms.json` | 5 |
| `src/OvermindCrewKit/KIT-MEMBERSHIP.md` | 4 |
| `STRUCTURE.md` (layout, dependency and door rules) | 4 |
| `src/Overmind.Infrastructure/Kits/BundledSeedService.cs` | 3 |
| `src/Overmind.Infrastructure/Kits/KitActivationService.cs` | 2 |
| `src/Overmind.Core/Entities/SkillKit.cs` | 2 |
| `build/pack.sh` | 2 |
| `src/Overmind.Mcp/Registration/InstallCommand.cs` | 2 |
| `AgentDetector.cs`, `AgentInstructionFile.cs`, `OnboardManagedBlock.cs`, `OnboardBlockContent.cs` | 1 each |
| **Total** | **84 occurrences over 70 distinct `file:line` targets** |

Path-level (no line anchor) sources: `src/OvermindCrewKit/manifest.json`,
`src/OvermindCrewKit/kit-membership.json`, `src/Overmind.Infrastructure/Kits/IGlobalKitDeployer.cs`
(cited by path — the claims made from them are whole-file shape/absence observations), plus the
Overmind board read of feature 251 (2026-11-20, recorded in the task log).

Verification run at write time: every one of the 70 distinct targets was resolved against the
worktree and checked for a non-empty span at the cited lines (0 problems). Marker uses in the
body — the preamble legend and this index section excluded, re-measured by the T4 feature sweep:
3 × **live on main** (the three section 7 rows) and 3 × **dark until 251** (one section 7 row,
the 251-hole paragraph, the board-state paragraph); the preamble legend states each marker once
more, so a whole-file count reads 5 and 5 including this sentence.

