---
feature: 126
task: 03
slug: readonly-resolver-no-create
status: done
review: light
created: 2026-10-06
depends_on: 01, 02
brief: Flip the three ReadOnly doors (read_project_manifest, planning_feature_code, overmind_replies) to create=false with honest 404 guidance; contract pins in Mcp.Tests; mutating doors pinned cwd-only.
---

# Task 03 — Read doors flip + contract test pins

<goal>
Switch every ReadOnly door that resolves from cwd to the create-free path: read_project_manifest, planning_feature_code and overmind_replies post `{ cwd, create = false }`, and a host 404 surfaces as the honest guidance error with no retry. Mutating doors (delete_entry, overmind_notify) stay cwd-only and get a pin proving they cannot silently flip.
</goal>

<scope>
- `src/Overmind.Mcp/OvermindTools.cs` — `read_project_manifest` resolves create=false; description gains the never-creates sentence ("never creates a project row; an unregistered directory returns guidance instead").
- `src/Overmind.Mcp/FeatureCodeTools.cs` — `planning_feature_code` same flip + description sentence.
- `src/Overmind.Mcp/OvermindNotificationTools.cs` — `overmind_replies` same flip + description sentence.
- Tests:
  - `tests/Overmind.Mcp.Tests/ProjectManifestToolContractTests.cs` — update `ReadProjectManifest_NoExplicitProject_ResolvesThroughTheOnboardCwd` to pin `create=false` in the resolve body (deliberate contract change, see obstacles); new test: stub 404 on POST /api/onboard → tool isError, text carries guidance, exactly one resolve call (no retry).
  - `tests/Overmind.Mcp.Tests/FeatureCodeToolContractTests.cs` — same pair of pins for planning_feature_code.
  - `tests/Overmind.Mcp.Tests/NotificationToolContractTests.cs` — create=false pin + 404 honest error for overmind_replies; a pin that overmind_notify's resolve body has NO `create` key.
  - Description pins updated where the three doors' descriptions are asserted.
</scope>

<anti-scope>
- No host changes (task 01 owns them); no resolver-shape changes (task 02 owns them).
- No behavior change for delete_entry / overmind_notify.
- No guidance text authored adapter-side — the adapter surfaces the host's 404 detail verbatim.
</anti-scope>

<constraints>
- The honest error rides the existing ApiNotFoundException plumbing — no new error machinery.
- Stub defaults (Canned.OnboardJson via RegisterDefaultRoutes) stay untouched; happy-path tests keep passing.
- English-only, no comments except non-obvious decisions.
</constraints>

<acceptance-criteria>
- [x] read_project_manifest without projectId posts create=false; 404 host → isError with guidance, exactly one resolve call.
- [x] planning_feature_code carries the same create-free pins (body + honest 404 error).
- [x] overmind_replies carries the same pins; overmind_notify's resolve body pinned to have no `create` key.
- [x] The three read-door descriptions state the never-creates contract; description pins updated.
- [x] `dotnet build` + `dotnet test tests/Overmind.Mcp.Tests` green.
</acceptance-criteria>

<refs>
- Spec: 29b2c8fe-bcb2-4ca8-904c-371a8fbc1288 (scope items 3–5, AC 4–8).
- Plan: 3e6b2cec-1e6b-4868-a844-a25367481f71, slice 03.
- Depends on: task 01 (host flag + guidance text) and task 02 (shared resolver).
</refs>

<obstacles>
- Existing test ReadProjectManifest_NoExplicitProject_ResolvesThroughTheOnboardCwd pins the resolve body as cwd-only — updating it is the deliberate contract change this task lands, not a regression (spec obstacles).
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan slice 03; blockedBy: tasks 01 and 02.
- 2026-10-06 [agent] — 126/03 coding round started
- 2026-10-06 [agent] — 126/03: manifest door flipped — create=false body pin + unregistered-cwd 404 guidance test with single resolve call
- 2026-10-06 [agent] — 126/03: feature_code door flipped — create=false body pin + 404 guidance throw with single resolve call, no git side effects
- 2026-10-06 [agent] — 126/03: replies door flipped; notify + delete_entry resolve bodies pinned to stay cwd-only (no create key)
- 2026-10-06 [agent] — 126/03: never-creates sentence added to all three read-door descriptions; manifest and replies description pins extended
- 2026-10-06 [agent] — 126/03: build clean (0 warnings), 517/517 Mcp.Tests green serially (-m:1), dotnet format no-op on changed files
- 2026-10-06 [agent] — delete_entry no-create-key pin landed in ToolContractTests.cs (DeleteEntry_NoExplicitProject_ResolvesThroughTheOnboardCwd) — outside the scope's listed test files, added because the goal mandates a pin proving mutating doors cannot silently flip; one assertion, no behavior change.
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 2 cosmetic suggestions (NRE-on-revert pin style; resolver file missing from scope's file list — artifact note for the next round). Flip verified against every call site (3 read doors create:false, 2 mutating cwd-only pinned), 404 travels verbatim via existing plumbing, mutation-checked pins both directions, wrinkle judgments accepted (delete_entry pin file + stale comment removal), spot-check 254/254 serial. Commit 0db6dc9.
</log>