---
feature: 013
slug: mcp-agent-onboarding
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Spec 013 — MCP agent onboarding ceremony (discover + remember project ID)

<decisions>
- Repo↔project binding: the adapter knows its cwd (stdio inheritance from the spawning agent) and reports it to the host at onboard time; the host matches by Project.DiskPath. Explicit env override (OVERMIND_PROJECT_ID) rides along for exotic layouts (symlinked dirs, worktrees) but cwd-matching is the default path (decision 2026-09-19).
- install --onboard writes the managed block itself (markers <!-- overmind:begin/end -->) into the target agent file (CLAUDE.md/AGENTS.md per agent kind), creating the file if absent, and WARNS on conflicting file-based ceremony instructions it finds — it never rewrites human content (decision 2026-09-19).
- No project match → auto-create from the directory name and say so in the response ("created project X"). A wrong project is one UI click to delete; a broken onboarding is a silent adoption killer (decision 2026-09-19).
- The onboarding response is rich: projectId + board summary (feature counts by status, current feature) + ceremony quickstart (which tools in which order). One round-trip and the agent is in context (decision 2026-09-19).
- Managed block content = override semantics: "this repo is Overmind project X (id …); planning goes through the overmind MCP tools; this block overrides any file-based ceremony instructions in this file" (recorded in the feature body after the Squeezr/Mindlash dogfood).
</decisions>

<open-questions>
- none
</open-questions>

<requirements>
Adapter (src/Overmind.Mcp):
- New MCP tool `overmind_onboard` (additive — contract allows additions; no renames): no required args; the adapter resolves its own Environment.CurrentDirectory (plus optional env override OVERMIND_PROJECT_ID), calls the host, returns the rich payload as text.
- The tool is listed in the adapter's tool docs/descriptions so agents discover it (the Squeezr lesson: available ≠ used — the tool description must say "call this first").

Host (src/Overmind.Host + Infrastructure/Planning):
- New endpoint POST /api/onboard { cwd } (additive): resolve project by DiskPath (normalize: full path, trailing slash, symlink resolve best-effort); miss → auto-create Project (name = directory name); response { projectId, projectName, created, board summary (counts by status, current feature slug), quickstart text }.
- The quickstart text is a single source (code constant or resource) — the MCP tool and the endpoint share it; no drift between doors.

Installer (overmind-mcp install, extends 009):
- New flag `--onboard`: after wiring the agent's MCP config, also writes/updates the managed block in the agent file at the target repo (CLAUDE.md for claude, AGENTS.md for kimi/others — per the existing 009 registry table), creating the file if absent. Block carries: project name + id (resolved via the host if running, else left as "resolve on first onboard call"), the MCP-first override line, the markers.
- Conflict detection: if the agent file contains file-based ceremony instructions (BACKLOG/ workflow mentions), print a loud warning listing the conflicting lines; never edit them.
- Idempotent: re-running install --onboard replaces the managed block between markers, touches nothing else.
- Registry update (009's table): each agent kind needs its agent-file convention (filename candidates) — extend the existing registry rather than inventing a parallel one.

Tests + dogfood:
- Onboard endpoint: match by DiskPath, normalization (trailing slash/casing on linux), auto-create, summary contents.
- Installer: managed block write/update/idempotency/conflict warning on fixture agent files.
- Live dogfood on closeout: a fresh Claude/Kimi session in a second repo onboards without a human touching its config.
</requirements>

<acceptance-ideas>
- Adapter spawned from /mnt/Ubuntu-Dev/Projects/AI-Tools/Squeezr → overmind_onboard returns the Squeezr project id + board summary + quickstart (live).
- Adapter spawned from a repo unknown to the host → project auto-created, response says created:true.
- `overmind-mcp install claude --onboard` in a repo with an existing CLAUDE.md prescribing file BACKLOG → managed block appended between markers, warning printed, human content untouched; second run replaces only the block.
- dotnet test green incl. new tests; MCP tool count grows by one (additive); smoke.sh stays green.
</acceptance-ideas>
