---
feature: 013
slug: mcp-agent-onboarding
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Plan 013 — MCP agent onboarding ceremony (discover + remember project ID)

<approach>
Three doors, one shared brain. The host owns the logic (project resolution by cwd, auto-create, board summary, quickstart text) behind a single additive endpoint `POST /api/onboard`. The MCP adapter adds a thin `overmind_onboard` tool that reports its own cwd (stdio inheritance) and passes the host's rich payload through verbatim — the adapter references no other project, so the quickstart text lives ONLY on the host and the tool never duplicates it. The installer (`overmind-mcp install --onboard`) is the file-side door: it extends the 009 agent registry with per-agent instruction-file conventions and writes a managed block (markers `<!-- overmind:begin/end -->`) into the repo's agent file, with loud conflict warnings and full idempotency. Closeout is a live dogfood: a fresh agent session in a second repo onboards with zero human config edits.

Order: endpoint first (02 and 03 both depend on the contract), then tool and installer in either order, then closeout. Everything is additive — the Host↔MCP contract allows additions, no renames/removals of existing DTO fields.
</approach>

<contract>
- `POST /api/onboard` — request `{ cwd: string, projectId?: guid }` (projectId = explicit override path for the env-var case). Response `{ projectId, projectName, created, board: { countsByStatus, currentSlug|null }, quickstart: string }`. Additive route, no auth (loopback boundary unchanged).
- cwd normalization: full path via Path.GetFullPath, trailing-slash trim, OrdinalIgnoreCase NOT applied (linux host; case-sensitive compare), best-effort symlink resolve. Match against Project.DiskPath.
- Miss → auto-create: name = directory name; on name collision with a different path, suffix `-2`, `-3`, … until unique. created:true in the response.
- Quickstart text: single constant on the host (resource/static class), returned in the payload; the MCP tool surfaces it unchanged.
- MCP tool `overmind_onboard` (no required args): adapter reads Environment.CurrentDirectory; env `OVERMIND_PROJECT_ID` (valid guid) wins over cwd and is sent as projectId. Tool description opens with "Call this first" — the Squeezr lesson: available ≠ used.
- Managed block format: `<!-- overmind:begin -->` … `<!-- overmind:end -->`, content = project name + id (or "resolved on first overmind_onboard call" when the host was unreachable at install time), the MCP-first override line, the onboard instruction. Never touches text outside the markers.
</contract>

<tasks>
- [ ] 01-onboard-endpoint — host endpoint + auto-create + board summary + quickstart constant; Api.Tests coverage → tasks/01-onboard-endpoint.md
- [ ] 02-onboard-mcp-tool — adapter tool + env override + "call this first" description + ServerInstructions mention; Mcp.Tests → tasks/02-onboard-mcp-tool.md
- [ ] 03-install-onboard-flag — `--onboard` flag, registry agent-file table, managed block writer, conflict detection, idempotency; Mcp.Tests with fixture agent files → tasks/03-install-onboard-flag.md
- [ ] 04-live-dogfood-closeout — pack 0.1.7, deploy, fresh-agent dogfood in Squeezr, materialize, result artifact → tasks/04-live-dogfood-closeout.md
</tasks>

<risks>
- Symlinked repo paths (~/apps/overmind is itself a symlink — same pattern may exist in target repos): resolved best-effort; env override is the escape hatch. Mitigation: document OVERMIND_PROJECT_ID in the tool description and quickstart.
- Name-collision auto-create could spawn junk projects ("-2" suffixes) if DiskPath in the DB drifts from the real path (e.g. repo moved). Accepted: one UI click to delete, per the spec decision; surfacing created:true keeps it visible.
- install --onboard when the host is down: block must still be written (id placeholder), warning printed; do NOT fail the whole install for it — onboarding degrades, registration still lands.
- Agent-file conventions beyond claude/kimi are guesses (codex/qwen/gemini/antigravity): pick the community-standard name per agent and record it in the registry so corrections are one-line.
- Existing managed-block mechanics: Infrastructure/Planning/ManagedBlock.cs already exists for materialization — reuse the marker pattern, do NOT invent a second block engine; if the installer needs its own writer (adapter references nothing), keep the two implementations structurally aligned and note it.
</risks>

<verification>
- dotnet test Overmind.sln green incl. new tests (baseline 735).
- Tool count grows by exactly one (additive contract); smoke.sh green.
- Live: adapter spawned with cwd=/mnt/Ubuntu-Dev/Projects/AI-Tools/Squeezr → overmind_onboard returns the Squeezr project id + board summary + quickstart.
- Live: `overmind-mcp install claude --onboard` in a repo with a file-ceremony CLAUDE.md → managed block appended, loud warning, human content untouched; second run replaces only the block.
- Live: fresh agent session in a second repo onboards with zero human config edits (the feature's acceptance dogfood).
</verification>