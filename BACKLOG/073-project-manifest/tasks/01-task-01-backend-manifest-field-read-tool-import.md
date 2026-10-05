---
feature: 073
task: 01
slug: project-manifest
status: done
review: light
created: 2026-10-04
brief: Backend: Manifest field + migration + read_project_manifest tool + import with precedence + tests
---

# Task 01 — Backend: manifest field + read tool + import

<goal>
Storage, agent read path, and one-way import for the manifest.
</goal>

<scope>
- Project.Manifest (nullable markdown) + migration AddProjectManifest; update-door contract (field on update request/response).
- MCP read_project_manifest (projectId | cwd-resolved; absent → honest «not set», not an error).
- Import-manifest action: {diskPath}/AGENTS.md → CLAUDE.md → QWEN.md first-found, header line records the source, PathGuard containment.
- Tests: round-trip, precedence, containment 400, honest-absent.
</scope>

<acceptance-criteria>
- [x] Round-trip + migration green — pinned.
- [x] Read tool honest-absent + cwd-resolve — contract-tested.
- [x] Import precedence AGENTS→CLAUDE→QWEN + header line + containment 400 — pinned.
</acceptance-criteria>

<refs>
- Spec 722d3acd; plan c6958bdf (task 1); 041 listing discipline.
</refs>

<log>
- 2026-10-05 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching dotnet-coder (sonnet).
- 2026-10-05 [agent] — Coding round complete (dotnet-coder, sonnet; gates re-verified by orchestrator — build 0w, Core 350 (+5), Infra 659 (+9), Api 642 (+7), Mcp 464 (+5), format clean; checkpoint ea08eac): migration 20261005153758_AddProjectManifest (TEXT nullable, FTS untouched — Projects has no triggers, precedent followed), update semantics setManifest (absent/false preserves, true+text sets trimmed, true+null clears — legacy-safe), import POST /api/projects/{id}/manifest/import (AGENTS→CLAUDE→QWEN first-found, empty candidates skipped, source header comment, PathGuard containment; relative diskPath → 400 via MaterializationErrors.Map; >200K → 400), MCP read_project_manifest (projectId | env | onboard cwd-resolve; honest not-set text; verbatim markdown response for token economy; 200K judgment call at Project.MaxManifestLength). Handshake tool count 41→42 pinned. Dispatching light review.
- 2026-10-05 [agent] — [agent] Light review PASS-WITH-NITS (audits/review-073-01.md; gates re-run: build 0w, Core 350, Infra 659→659, Api 642, Mcp 464, format clean). 200K cap ruled PASS (both doors cap the same stored composite, entity backstop). N2 fixed in-tree (121ba0d): oversized candidates fail loud with a 400 naming the candidate (InvalidTarget factory added) instead of buffering multi-GB reads AND instead of silently importing a lower-priority file's manifest. W1 ruled pre-existing shared behavior: read_project_manifest's cwd-resolve goes through ResolveProjectIdAsync → onboard, which CREATES a project row on unmatched cwd — ReadOnly contract lie inherited by entry-delete and notification tools; review recommends a create-free resolve variant as a board follow-up (ledger #231). N1 judged ok (fail-loud on unreadable candidates beats silent second-priority import). All 3 AC ticked. Closing task.
</log>
