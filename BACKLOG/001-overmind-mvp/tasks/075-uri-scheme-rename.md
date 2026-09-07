---
feature: 001
task: 7.5
slug: uri-scheme-rename
status: done
review: light
created: 2026-09-07
---

# 001/7.5 — URI scheme rename: ctx:// → overmind://

<goal>
The custom MCP resource URI scheme `ctx://` is renamed to `overmind://` across code, tests, and documentation. All builds and tests stay green.
</goal>

<context>
User decision (2026-09-07): `ctx://` is a generic scheme that can collide with other MCP servers a user has installed; `overmind://` is self-documenting, collision-free, and on-brand. Window is ideal: MVP, no external consumers, and materialized agent instructions referencing the scheme are only in the user's own projects (re-materialization fixes drift). Spec R2.2 and the MCP-engine decision must be updated with a dated change note — spec.md is frozen, so the edit is accompanied by an explicit decision-change record, not a silent edit.
</context>

<scope>
- Code: `src/Overmind.Mcp/OvermindResources.cs` (8 UriTemplates), `src/Overmind.Mcp/Program.cs`, `src/Overmind.Mcp/SessionBootstrapPrompt.cs`, `src/Overmind.Infrastructure/Materialization/SkillMaterializer.cs` — mechanical rename `ctx://` → `overmind://`.
- Tests: `tests/Overmind.Mcp.Tests/HandshakeContractTests.cs`, `ResourceContractTests.cs`, `PromptAndFailureModeTests.cs`, `tests/Overmind.Api.Tests/KitApiTests.cs`, `tests/Overmind.Infrastructure.Tests/MaterializationTests.cs`.
- Docs: `BACKLOG/001-overmind-mvp/spec.md` (lines 20, 42, 45, R2.2) — rename + add dated decision-change note; `plan.md`; `tasks/04`, `tasks/06`, `tasks/08` mentions; `README.md`; `BACKLOG.md`.
- ADR: add an entry to `TECHNICAL-SOLUTIONS.md` — custom MCP resource scheme `overmind://`, rationale (collision avoidance, branding), supersedes the original `ctx://` decision.
</scope>

<anti-scope>
- No behavior changes — pure rename of the scheme string and doc references.
- No changes to resource names/templates structure (`projects/{id}/gates` etc. stay).
- No migration for previously materialized files (drift + re-materialization covers it).
</anti-scope>

<constraints>
- `dotnet build` and `dotnet test` must pass fully after the rename.
- Doc edits in frozen spec.md get a dated change note.
- 62 known occurrences across 16 files (grep `ctx://` at creation time) — verify zero remain via grep before finishing.
</constraints>

<acceptance-criteria>
- [ ] `grep -r "ctx://"` in repo returns zero matches (excluding git history).
- [ ] `dotnet build` and `dotnet test` — green.
- [ ] spec.md updated with dated decision-change note; TECHNICAL-SOLUTIONS.md has the ADR entry.
</acceptance-criteria>

<refs>
- @../spec.md — R2.2, decisions (custom URI scheme)
- @../plan.md
- tasks/06-mcp-adapter.md — resources implemented there
</refs>

<obstacles>
<!-- Filled by executors during work -->
</obstacles>

<log>
2026-09-07 — task created from user decision: rename ctx:// → overmind:// (vibe + MCP scheme collision avoidance).
2026-09-07 — executed. 62 occurrences re-measured as 70 across 17 files at start (tasks/06 grew since task creation); renamed in 4 source files + 5 test files + 8 doc files. spec.md got two dated change notes (decision line + R2.2); ADR added to TECHNICAL-SOLUTIONS.md (top, newest-first). Remaining `ctx://` grep hits are intentional: this task file's own record and the dated change notes referencing the rename. Build + tests green.
</log>
