```
{  ·  ·  ·  ·  ·  }
 |    .-----.    |
· ----(  ·  )---- ·
 |    '-----'    |
{  ·  ·  ·  ·  ·  }

 ██████╗ ██╗   ██╗███████╗██████╗ ███╗   ███╗██╗███╗   ██╗██████╗
██╔═══██╗██║   ██║██╔════╝██╔══██╗████╗ ████║██║████╗  ██║██╔══██╗
███████╗██║   ██║█████╗  ██████╔╝██╔████╔██║██║██╔██╗ ██║██║  ██║
██║   ██║╚██╗ ██╔╝██╔══╝  ██╔══██╗██║╚██╔╝██║██║██║╚██╗██║██║  ██║
╚██████╔╝ ╚████╔╝ ███████╗██║  ██║██║ ╚═╝ ██║██║██║ ╚████║██████╔╝
 ╚═════╝   ╚═══╝  ╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝╚═╝╚═╝  ╚═══╝╚═════╝
```

**The board each project thinks on.**  
*The circle you draw around one summoning.*

`await SummonAsync();` — part of the summoner's kit  
**▸ overmind** · grimoire · squeezr · mindlash studio

---

## What it is

Overmind is project intelligence for one project: the planning board, tasks,
decisions, gates, guards and skill kits, served to every MCP-capable agent
from one local process. Claude Code, Kimi Code, Codex, Gemini CLI,
Antigravity — they all stop being re-explained the same architecture in
every session, because the context lives here: notes, architectural
decisions, gates, guards and skill kits in one local database.

A desktop app (Windows/macOS/Linux, .NET). One tray process hosts a loopback
HTTP API and a web UI — the browser is the window. Agents attach through a
thin stdio MCP adapter with one config line; context is addressable through
the `overmind://` URI scheme (`overmind://projects/<id>/gates`).

- **Skill kits** — named sets of skills/gates/guards. Activate a kit on a
  project and the app materializes the files into `.claude/skills/`,
  `.kimi-code/skills/` and the rest. The database is the source of truth,
  the disk is a managed cache — switch the kit, the files rebuild.
- **Search that always works** — SQLite FTS5 out of the box, offline, zero
  dependencies. Want semantics — bring up Qdrant in Docker and pick an
  embedding provider in settings (local ONNX / Ollama / OpenAI-compatible).
  No Docker — an honest "vector search is off" banner, and FTS keeps
  working.
- **Projects as a disk tree** — the sidebar shows the real folder tree:
  plain folders dimmed, projects clickable, register-in-place. The user sets
  the tree roots; clicking a project makes it the active context for
  everything that follows.

## Why it exists

One day a developer saw
[OpenViking](https://github.com/volcengine/OpenViking) — Volcengine's
context base for agents with the `viking://` filesystem paradigm. The idea
landed so well he wanted his own. What did not land was the setup: external
services, keys, configs — too many movements for "remember what we
decided".

So the plan became: the same idea, but **download-and-run**. Download, run,
it works. No mandatory Docker, no keys, no quests.

## The name

Chosen by a four-round tournament. The finalists: `shared-braincell`,
`the-agents-dungeon`, `mimirs-head`, `the-construct` and other worthy
contenders. **Overmind** won — the mind of the StarCraft swarm that commands
the zergling broods. Because that is exactly what happens: your Claudes and
Kimis are the zerglings, and here they finally share a head.

## Quickstart

```bash
dotnet run --project src/Overmind.Host
```

The host is one process: single-instance check, SQLite, port pick, Kestrel,
tray. Running it again while an instance is alive does not spawn a second
process — it opens the browser on the existing one and exits.

What happens at startup: `~/.overmind/` is created → the SQLite database
(migrations applied automatically) → the port (4761 by default, else the
first free one) → the port is written to `~/.overmind/host.port` → Kestrel →
the tray icon. Tray menu: **Open Overmind** (web UI in the default browser)
and **Quit** (a planned stop, the port is released). Ctrl+C / SIGTERM stop
it too. Any startup failure — a console message and a non-zero exit code.

The UI opens at `http://overmind.localhost:<port>` — the `.localhost`
hostname resolves to loopback in every modern browser with no setup. The
API and the MCP adapter keep talking to `127.0.0.1` directly.

- **Database**: `~/.overmind/overmind.db` (SQLite + FTS5, created for you).
- **Port file**: `~/.overmind/host.port` — how the MCP adapter and a second
  launch find the live instance.
- **Headless** (no tray, for debugging/CI): `dotnet run --project
  src/Overmind.Host -- --no-tray`.
- **Known macOS limitation**: the tray icon does not appear when launched
  via `dotnet run` outside a signed `.app` bundle. Packaging is post-MVP.

## Trade-offs

Read these before you adopt. They are the same weight as the feature list —
that is the deal.

- **Local and loopback by design**: no multi-user, no team server, no
  remote access. One machine, one summoner.
- **The ceremony is deliberate overhead.** For a one-line fix, a full
  spec → plan → tasks cycle is too much — the tool is for work that outlives
  a session.
- **Value scales with how many MCP-capable agents you actually run.** With
  a single agent and a small repo, a README does most of the job.
- **Vector search needs Docker + Qdrant + an embedding provider.**
  Optional, but "optional" still means extra setup for the good search.
- **Desktop-first.** The tray app is the entry point, which is unusual for a
  tool people expect to be a CLI or a SaaS.

## Attach an agent (MCP)

MCP client configuration example (Claude Code, Kimi Code, etc.):

```json
{
  "mcpServers": {
    "overmind": {
      "command": "dotnet",
      "args": ["run", "--project", "src/Overmind.Mcp"],
      "env": { "OVERMIND_PROJECT_ID": "<project-id>" }
    }
  }
}
```

`OVERMIND_PROJECT_ID` — the project id (visible in the UI or via
`GET /api/projects/`); without it the adapter takes the only existing
project. No API address needed: the adapter finds the host through
`~/.overmind/host.port`. The agent reads context through the URI scheme:
`overmind://projects/<id>/gates`, `overmind://projects/<id>/guards`, and so
on.

### Idempotent mutation retries (X-Operation-Id)

The mutating planning tools accept an optional `operationId` parameter (a
GUID): `planning_upsert_artifact`, `planning_upsert_tasks`,
`planning_patch_artifact`, `planning_set_status`, `planning_append_log`,
`planning_edit_log`, `planning_set_feature_status`,
`planning_update_feature`, `planning_set_feature_priority`. It travels as
the `X-Operation-Id` header, and the Host keeps a response registry (24
hours):

- **same `operationId` + same payload** → the Host returns the stored
  answer with `"replayed": true`; nothing is re-applied, the version does
  not grow;
- **same `operationId` + a different payload** → `409
  OperationIdConflict` — mint a fresh id per logical operation;
- **not a GUID** → `400 InvalidOperationId`;
- only 2xx answers are stored — a failed mutation is not remembered, a
  retry with the same id executes for real.

The retry contract for an agent: a transport failure always carries the
marker `retryable transport failure:` — only this error class is worth
retrying, and only by repeating **the same** call with the same
`operationId`. Business errors (4xx, `STOP:` gates) carry no marker —
retrying them is pointless. With an `operationId` a retry is idempotent in
any outcome: the Host either replays the stored answer or honestly answers
`409`. The error text after the marker may add the adapter's read-back
verdict ("write committed at vN — do not resend" / "no trace" / "outcome
unknown") — with a live `operationId` it changes nothing, replay protects
anyway.

The wire is compatible both ways: without `operationId` the header is not
sent at all; an **old Host** simply ignores the unknown header; a **new
Host with an old adapter** works as before. No lockstep required — but
replay is only possible when both sides are new.

## Windows: the installer

The ready installer — `overmind-<version>-setup.exe` (Inno Setup), built by
`build/windows/pack-win.ps1` on a Windows machine.

- **Download and SmartScreen.** The build is unsigned (no certificate — a
  personal tool, not a commercial product), so Windows shows "Windows
  protected your PC" / SmartScreen on first run. Expected, not a bug:
  **More info** → **Run anyway**.
- **The wizard.** Default — per-user install
  (`%LocalAppData%\Programs\Overmind`, no admin rights); an option exists
  for a per-machine install (`C:\Program Files\Overmind`, requires UAC).
  Four wizard tasks: **run at login** (on by default — adds `Overmind` to
  `HKCU\...\Run`), **add `overmind-mcp` to PATH** (on by default — so
  `overmind-mcp` runs from any shell by its short name), **register
  Overmind with the AI agents on this machine** (on by default — runs
  `overmind-mcp install`, which **writes to agent config files**: the
  adapter detects which of the six supported agents are actually installed
  and edits only those configs; typically that is Claude Code and/or Kimi
  Code — the exact set depends on what you have installed), **desktop
  shortcut** (off by default). A Start Menu shortcut is always created.
  Registration can be added or removed manually at any time: `overmind-mcp
  install` / `overmind-mcp install --remove` (lives in `{install
  dir}\mcp\`, available as `overmind-mcp` from any shell when the PATH task
  is on). **An already-running agent will not see the new registration** —
  it reads its config at startup, so after installing (or a manual
  `install`) the agent (e.g. Claude Code) needs a restart. **Per-machine
  install caveat:** confirm the UAC prompt with your own admin account, not
  someone else's — autostart, the port-file search during upgrade and the
  uninstall data dialog all key off `%USERPROFILE%`/`HKCU` of the account
  that accepted the elevation, not the account you actually work from; if
  those differ, autostart quietly registers in the wrong profile and the
  graceful Host stop during an upgrade silently does nothing.
- **Uninstall and agent registration.** Before removing files, the
  uninstaller itself calls `overmind-mcp install --remove`, unregistering
  from the same agents — so after removal no agent keeps an entry pointing
  at an exe that no longer exists. If the registration/unregistration
  itself fails (e.g. the config file is locked by another process) — it
  blocks neither install nor uninstall: the app works perfectly fine
  without agent registrations, that is just a convenience.
- **Upgrading.** Running `setup.exe` again over an existing install is an
  in-place upgrade: the installer stops a running Host itself via
  `POST /api/shutdown` (a planned stop, not `taskkill`) before replacing
  files, then may restart it.
- **Uninstalling.** Through "Apps & features" / Control Panel, like any
  program. The installer asks in a separate dialog whether to delete user
  data (`%USERPROFILE%\.overmind` — the database, settings, downloaded
  ONNX models); **the default is No**, the data stays on disk for the next
  install. A silent uninstall (`/VERYSILENT`) never touches `~/.overmind`
  unless `/DELETEDATA` is passed explicitly.
- **User data** always lives in `%USERPROFILE%\.overmind`, never in the
  install dir — it survives both an upgrade and a plain uninstall.

## Status

Alpha. The MVP is shipped (`001-overmind-mvp`, 9 tasks: HTTP API, skill
kits with materialization, FTS + optional semantics, the MCP adapter, the
web UI, the tray host), with `004-projects-tree-view` and
`005-tree-workspace` on top. `006-planning` is in flight — the feature →
spec → plan → task → result ceremony as a first-class entity (3 of 11
tasks done: domain, schema, the human door API). Next: `002-kit-library`,
then `003-bundled-installer`. The live slice is the planning board (web UI
or MCP); `BACKLOG/` is the materialized mirror of the contracts.

The latest wave (`137`) shipped parallel-wave coordination — lane rosters,
claim-time lane checks, a one-call wave snapshot, the directive channel and
entry read-acks — bringing the MCP surface to 55 tools.

Token-cost attribution is landing slice by slice: `POST /api/usage` ingests
token records and `GET /api/usage/summary` rolls them up per feature through
claim spans and per agent. Dollars are computed at read time from the pricing
table in `GET/PUT /api/settings/usage-pricing` — the defaults cover the Claude
rows (opus/sonnet/haiku, 2026 prices) and the table is meant to be edited for
any other model you run.

## Token usage attribution

Overmind can track how many tokens each agent session burns and attribute
that spend to the feature the session was claiming. The pieces:

- **Ingest** — `POST /api/usage` accepts one record
  (`alias`, `sessionId`, `ts`, `model`, the four token counters). Unknown
  aliases and sessions are stored too and simply stay unattributed.
- **Attribution** — a record lands on a feature when its `alias` + `sessionId`
  match a claim span (`FeatureClaimSpan`) and its `ts` falls inside the span's
  `ClaimedAt .. ReleasedAt` window. Spans exist from the moment this feature
  shipped — there is no retroactive attribution for claims made before
  the spans table was introduced, and sessions reported under a different
  session-id space (for example a Claude Code session id) do not join
  Overmind claim sessions.
- **Pricing** — dollars are computed at read time from the pricing table, so
  changing prices reprices history. Defaults ship for the Claude models; edit
  the table via the settings door:

  ```bash
  curl -X PUT http://127.0.0.1:4761/api/settings/usage-pricing \
       -H 'Content-Type: application/json' \
       -d '{"pricing": {"glm-5": {"input": 1.5, "output": 7.0,
                                   "cacheRead": 0.15, "cacheWrite": 0.3}}}'
  ```

  Prices are per 1M tokens. An empty table disables dollar math (tokens are
  still counted).

- **Stop hook** — `scripts/usage-stop-hook.sh` reports per-turn usage from a
  Claude Code session: on every Stop event it sums the assistant usage
  appended to the session transcript since the previous Stop and posts it to
  `POST /api/usage`. It never blocks the session — if Overmind is down or the
  transcript is unreadable it exits silently and retries the window on the
  next Stop. **Activation is a manual, human-only step**: wiring a hook edits
  your own `~/.claude/settings.json`, which agents must never touch. The
  snippet below is documentation, not something an agent should apply:

  ```json
  {
    "hooks": {
      "Stop": [
        {
          "hooks": [
            { "type": "command",
              "command": "bash /absolute/path/to/overmind/scripts/usage-stop-hook.sh" }
          ]
        }
      ]
    }
  }
  ```

  The alias comes from `OVERMIND_ALIAS` (falling back to git `user.name`),
  the base URL from `OVERMIND_HOST` (default `http://127.0.0.1:4761`).

## Roadmap

| Prio | Scope | Status |
|------|-------|--------|
| v1 (MVP) | Multi-project context store, notes/decisions/gates/guards/skills, FTS + optional semantics, tray + web UI, stdio MCP adapter, kit materialization into projects | ✅ shipped (`001`) |
| Projects tree | Sidebar tree of real folders, register-in-place, user roots + active project | ✅ shipped (`004`, `005`) |
| Planning ceremony | feature → spec → plan → task → result in the DB, agent door + MCP surface, web editor, repo materialization (off/on/archive), import of an existing `BACKLOG/` | 🔄 in work (`006`, 3/11) |
| Kit library | Import kits from a local folder or a git reference (private repos included, credentials preserved) | ⬜ planned (`002`) |
| Installer | win/mac installer with curated kits bundled, rides the `002` import pipeline | 🔒 blocked (`003`) |
| P1 | Kit materialization into an external folder + an agent launcher (`claude --add-dir`, `kimi --skills-dir`) | ⬜ deferred |
| P2 | Runtime skill delivery over MCP (`get_skill`, `overmind://skills/`, a bootstrap skill) | ⬜ deferred |
| P2/P3 | Agent initialization flow, project onboarding | ⬜ deferred |

## Repository layout

```
BACKLOG/nnn-slug/             one folder per feature
  feature.md                  what and why
  spec.md                     validated design decisions and requirements (frozen)
  plan.md                     validated implementation plan (frozen)
  tasks/NN-slug.md            execution contract per task
CONTEXT.md                    system design: components, communication rules, AI framework
STRUCTURE.md                  layout + architecture rules + code conventions (read before writing code)
TECHNICAL-SOLUTIONS.md        ADRs: what was chosen over what, and why
audits/                       skill-produced reports, named <skill>-*.md
design/                       brand and sprite sources
src/Overmind.Core/            domain: entities, enums, abstractions — zero dependencies
src/Overmind.Infrastructure/  EF Core (SQLite + FTS5), vector search (Qdrant), embeddings, materialization, planning services
src/Overmind.Host/            composition root: loopback Kestrel, minimal APIs, tray, SPA hosting
src/Overmind.Mcp/             stdio MCP adapter — no project references, talks HTTP to the Host
tests/                        mirrors src: Core / Infrastructure / Api / Mcp tests
web/                          Next.js 16 static-export SPA (FSD), served by the Host
```

## License

TBD

---

*summonasync — one dev, a swarm of agents, nothing leaves the machine.*  
overmind · grimoire · squeezr · mindlash studio

Everything here runs on your machine: the database lives in `~/.overmind`,
the HTTP face binds loopback only. If it does not — that is a bug, file it.
