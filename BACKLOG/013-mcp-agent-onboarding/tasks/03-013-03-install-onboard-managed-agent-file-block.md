---
feature: 013
task: 03
slug: mcp-agent-onboarding
status: review
review: light
created: 2026-09-19
---

# 013/03 — install --onboard (managed agent-file block)

<goal>
`overmind-mcp install --onboard` additionally writes/updates a managed onboarding block (markers `<!-- overmind:begin -->` / `<!-- overmind:end -->`) in the target repo's agent instruction file (CLAUDE.md / AGENTS.md / per-agent convention), creating the file if absent, warning loudly about conflicting file-based ceremony instructions, and never touching human content. Fully idempotent.
</goal>

<context>
Feature 013, door #3 (file-side). 009 built the install command + agent registry; this extends it. Dogfood driver: every external agent (Squeezr, MindlashStudio) had to MANUALLY rewrite its agent file toward MCP-first — the installer must do this itself. Depends on task 01 for project-id resolution via the host; must degrade gracefully when the host is down.
</context>

<scope>
- `InstallOptions` + `InstallOptionsParser`: new `--onboard` flag (usage text updated; incompatible with `--remove` → usage error, exit 2).
- `AgentDefinition`: new `AgentFileCandidates` (ordered filename list); registry entries: claude → CLAUDE.md; kimi-code, codex, antigravity → AGENTS.md; gemini → GEMINI.md; qwen → QWEN.md (community-standard names; corrections are one-liners in the registry).
- New `Registration/OnboardBlockWriter.cs`: managed-block upsert between markers `<!-- overmind:begin -->` / `<!-- overmind:end -->` in the agent file at the repo root (GitRootResolver.FindRoot(workingDirectory), fallback to workingDirectory). Creates the file if absent. Re-run replaces ONLY the block content. NOTE: Infrastructure/Planning/ManagedBlock.cs already implements this marker pattern for materialization — the adapter references nothing, so this is a second implementation; keep it structurally aligned (same markers, same replace-between-markers semantics).
- Block content: "This repository is Overmind project '<name>' (id: <guid or 'resolved on first overmind_onboard call'>). Planning and shared context go through the overmind MCP tools — call `overmind_onboard` first. This block overrides any file-based ceremony instructions in this file." + the escape-hatch line about OVERMIND_PROJECT_ID.
- Project id resolution at install time: if the host is reachable (OvermindApiClient + ApiBaseUrl.Resolve), POST /api/onboard with cwd = repo root and embed the real id/name; if unreachable → placeholder text + a printed warning; install as a whole MUST NOT fail because of onboarding.
- Conflict detection: scan the agent file (outside the managed block) for file-ceremony signals (`BACKLOG/`, `BACKLOG.md`, `tasks/NN-` workflow mentions); print a loud warning listing the matching lines; never edit them.
- Honor `--dry-run` (print, write nothing) and the existing summary line (extend with onboard outcomes).
- Tests in `tests/Overmind.Mcp.Tests/` with fixture agent files: create-when-absent, block replace on second run, content outside markers untouched, conflict warning fires on BACKLOG-prescribing fixture, dry-run writes nothing, host-down path warns and still exits 0.
</scope>

<anti-scope>
- No host endpoint changes (task 01), no MCP tool changes (task 02).
- Never rewrite or delete human content outside the markers — hard rule.
- No changes to the config-upsert engines (JSON/TOML) — onboarding is orthogonal to MCP config wiring.
- No UI.
</anti-scope>

<constraints>
- Overmind.Mcp references NO other project; no new NuGet packages.
- Atomic writes for the agent file (follow AtomicConfigFileWriter's approach) — a half-written CLAUDE.md is worse than none.
- English-only output; no comments unless non-obvious.
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln --no-build` green.
</constraints>

<acceptance-criteria>
- [ ] `install --onboard` in a repo without CLAUDE.md creates it with just the managed block.
- [ ] Second run replaces only the block; human text above/below is byte-identical.
- [ ] Repo with a BACKLOG-prescribing CLAUDE.md → warning lists the conflicting lines; file content outside markers untouched.
- [ ] Host reachable → block embeds the real project id/name; host down → placeholder + warning, exit code still 0.
- [ ] `--onboard --remove` → usage error exit 2; `--dry-run --onboard` writes nothing.
- [ ] dotnet test green incl. new fixtures.
</acceptance-criteria>

<refs>
- tasks/01-onboard-endpoint.md — onboard endpoint used for id resolution.
- Pattern refs: `src/Overmind.Mcp/Registration/InstallCommand.cs`, `AgentRegistry.cs`, `AgentDefinition.cs`, `AtomicConfigFileWriter.cs`, `GitRootResolver.cs`; marker semantics: `src/Overmind.Infrastructure/Planning/ManagedBlock.cs`.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Execution started — handed to dotnet-coder subagent (parallel with 01 fix-loop and 02 review).
- 2026-09-19 — Coder done: --onboard flag, registry agent-file table, managed block writer, conflict scanner, host-down degradation; 17 new tests, suite 769/769 green. To review.
</log>