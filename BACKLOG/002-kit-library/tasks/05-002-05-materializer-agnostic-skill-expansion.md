---
feature: 002
task: 05
slug: kit-library
status: done
review: hard
created: 2026-09-14
---

# 002/05 — Materializer: agnostic skill expansion

<goal>
A platform-agnostic `SkillFile` (the representation chosen in task 04) materializes to EVERY platform in `PlatformDirectoryMap` on kit activation — so an imported kit from one agent's folder works for all agents. Drift tracking covers the expanded paths. Per the task 01 spike verdict: frontmatter normalization per target platform only where real differences were proven, otherwise byte-identical pass-through.
</goal>

<context>
Today `SkillMaterializer.MaterializeAsync` writes, for each platform found in the kit's skill files, `<native-skills-dir>/<skill-slug>/<file-relative-path>` and tracks every file in `MaterializedFile` with a normalized SHA-256. After task 04, kits can contain agnostic skills with no explicit platform; without this task they materialize nowhere (or wrong). Read `tasks/01-format-spike.md` `<findings>` BEFORE coding — it decides whether normalization transforms are needed. Executor reads ONLY this file + the spike findings.
</context>

<scope>
- `src/Overmind.Infrastructure/Materialization/SkillMaterializer.cs` — agnostic skills expand to all platforms from `PlatformDirectoryMap`; platform-marked skills keep current behavior
- `MaterializedFile` rows + drift detection (`ContentHash`-based) cover every expanded path; deactivation removes expanded files
- If the spike verdict is `normalize`: implement exactly the transform list from `tasks/01-format-spike.md` `<findings>`, applied per target platform at write time, and record it in `<log>`. If `pass-through`: write byte-identical content (current pipeline) and record that
- Overwrite protection (untracked existing target path → 409 `OverwriteBlocked`) must work for expanded paths exactly as for explicit ones
- `platforms.json` maintenance per the task 01 spike findings (`tasks/01-format-spike.md` `<findings>`, "platforms.json gaps"): add the missing `qwen` mapping (`.qwen/skills`); VERIFY the `codex` entry against the Codex version Overmind targets — official docs list `$CWD/.agents/skills` (walk-up) and `$HOME/.agents/skills`, `~/.codex/skills` is community-sourced only; record the verified outcome in `<log>`
- Tests: activation with a mixed kit (agnostic + platform-marked skills) writes the right files per platform; drift detects edits in expanded files; deactivation cleans everything; existing materialization tests stay green
</scope>

<anti-scope>
- Do NOT change the materialized layout (`<kit-slug>/<skill-slug>/` double nesting) — known deferred hardening item, fixing it mid-feature is forbidden
- No planning materializer changes (`PlanningMaterializer`)
- No scanner/importer/re-import logic (tasks 03/04/06)
- No API or UI changes
</anti-scope>

<constraints>
- No new NuGet packages
- `dotnet build Overmind.sln` + `dotnet test tests/Overmind.Infrastructure.Tests` green
- No comments in code; no auto-commits
</constraints>

<acceptance-criteria>
- [x] Activating a kit with an agnostic skill writes it under every platform's native skills dir; a platform-marked skill lands only under its platform
- [x] Every expanded file has a `MaterializedFile` row; editing one on disk shows in drift; deactivation removes all expanded files
- [x] OverwriteBlocked fires for foreign files at expanded target paths
- [x] Spike verdict honored and recorded in `<log>` (pass-through or exact transforms)
- [x] All pre-existing materialization tests green
</acceptance-criteria>

<refs>
- @../spec.md — D3, R11 (reviewer reads, not executor)
- @../plan.md — task 05 row
- tasks/01-format-spike.md — verdict + transform list (READ FIRST)
- tasks/04-kit-importer.md — agnostic `SkillFile` representation
</refs>

<obstacles>
- Codex mapping left unchanged on purpose: contract says VERIFY + record, not change; and moving codex to `.agents/skills` has a cross-platform side effect (Kimi/Gemini also read `.agents/skills`) that needs a design decision, not a mid-task call. Recorded as P1 in BACKLOG.md Deferred.
- Frontend `AGENT_PLATFORMS` in `web/src/shared/api/types.ts` now lags the backend enum (no "qwen"); anti-scope forbids UI changes, and nothing crashes (string union, no exhaustive switch) — follow-up UI task should add the qwen tab.
- Same skill carrying an agnostic file and an explicit-platform file with the same RelativePath now collides at that platform's expanded target → `MaterializationException.DuplicateTarget` (pre-existing guard, now reachable via expansion). Considered correct: silent overwrite would be worse; import gates G1–G5 keep names canonical but say nothing about this mix — flag for the reviewer.
- `dotnet format --verify-no-changes` reports pre-existing IDE0161 violations in generated EF migration files only; none of the files touched here violate.
</obstacles>

<log>
2026-09-14 — sub-agent (K2.7) — task done. SPIKE VERDICT HONORED: `pass-through` — zero per-platform frontmatter transforms; existing write pipeline kept byte-for-byte (incl. its CRLF→LF normalization + UTF-8 no-BOM, which the pre-existing tests pin); agnostic files expand to all 6 platforms with identical content (asserted byte-equal in test).
2026-09-14 — sub-agent (K2.7) — platforms.json: added `qwen → .qwen/skills` + `Qwen` appended at the END of `AgentPlatform` enum (int values of existing members unchanged, no EF migration). `PlatformDirectoryMap.Load()` enumerates the enum and throws on a missing mapping, so enum+json had to land together; `KitScanner` picks up `.qwen/skills` recognition automatically through the map (no scanner code touched).
2026-09-14 — sub-agent (K2.7) — CODEX VERIFICATION OUTCOME: verified against official docs https://developers.openai.com/codex/skills/ (no version pinned anywhere in the repo → current docs are the target). Official skill locations: `$CWD/.agents/skills` (walk-up to repo root), `$HOME/.agents/skills`, `/etc/codex/skills`, bundled. `~/.codex/skills` does NOT appear; third-party bug report (github.com/pivoshenko/kasetto#48, 2026-07-19) confirms skills materialized to `.codex/skills` are silently invisible to Codex CLI. Current mapping `codex → .codex/skills` is therefore UNVERIFIED/broken for current Codex — left as-is per contract (verify+record only) + shared-alias side effect; P1 deferred line added to BACKLOG.md.
2026-09-14 — sub-agent (K2.7) — impl: `BuildPlans` expands `Platform == null` files per platform from `PlatformDirectoryMap.SkillsDirs` (new exposed read-only view); bootstrap now targets the union of platforms receiving skill files (explicit-only kits keep old behavior — no agnostic files → no qwen/codex dirs). Overwrite protection, drift, deactivation all flow through the existing plan/row machinery untouched. Materialized layout unchanged (double nesting intact per anti-scope).
2026-09-14 — sub-agent (K2.7) — tests: 4 new in MaterializationTests (mixed-kit expansion + platform exclusivity + byte-identical copies + row coverage; drift Modified/Missing on expanded paths; deactivation removes all 6 platform trees; OverwriteBlocked at an expanded `.qwen` target). `dotnet build Overmind.sln` clean (0 warn/0 err); `dotnet test tests/Overmind.Infrastructure.Tests` 154/154; full solution 441/441 green. CONTEXT.md enum line updated to include qwen.
</log>
