---
feature: 126
slug: readonly-resolver-no-create
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 126 — ReadOnly resolver: read tools never create project rows (create-free resolve path)

## What shipped

Read-only MCP tools no longer mint project rows as a side effect of cwd resolution.

- **Host create-free mode** (af6d052): `OnboardRequest` carries an optional `create` flag, default `true` — zero wire change for every existing caller (`overmind_onboard`, `overmind_notify`, install flows). With `create=false`, no explicit projectId and no `DiskPath` match, `POST /api/onboard` returns 404 with register-guidance quoting the normalized cwd and inserts nothing. The match path and the explicit-projectId path are byte-identical to before; `created=true` can now only mean "a row was minted this call".
- **Consolidated resolver** (d39cb54): the three near-identical private `ResolveProjectIdAsync` copies (`OvermindTools`, `FeatureCodeTools`, `OvermindNotificationTools`) collapsed into one injectable `ProjectResolver` singleton with a create parameter and preserved per-door error nouns ("for the entry" / "for the working directory" / "for the notification"). Behavior-neutral refactor, verified green across all five cwd-resolving doors before any flip.
- **Read doors flipped** (0db6dc9): the three ReadOnly doors that resolve from cwd — `read_project_manifest`, `planning_feature_code`, `overmind_replies` — post `{ cwd, create = false }`; a host 404 surfaces verbatim as the honest tool error (guidance to run `overmind_onboard` or pass `projectId`), exactly one resolve call, no retry. Descriptions state the never-creates contract.
- **Mutating doors pinned**: `delete_entry` and `overmind_notify` keep the cwd-only create-on-miss body — pinned so they cannot silently flip.

The `NormalizeCwd` round-trip (symlink resolution + trailing-separator trim) stays the single server-side matching truth; no client-side path matching, no new endpoint, no schema changes.

## Verification

- Final gate: full serial `dotnet test` — 2253 passed / 0 failed across all test projects; `dotnet build` green.
- Spec sweep: 8/8 acceptance items mapped to pins or no-change proofs and ticked.
- All 4 tasks light-APPROVED (01 host, 02 consolidation, 03 flip, 04 gate).
- Core pin: `create=false` + unregistered cwd → 404, projects count before == count after (`OnboardApiTests`); adapter contract pins in `ProjectManifestToolContractTests` / `FeatureCodeToolContractTests` / `NotificationToolContractTests`.

## Decisions

- Guidance text is authored once, host-side (the 404 detail), and surfaced by the adapter verbatim — no diverging copies across doors.
- Create-free rides the existing onboard door via a default-true flag rather than a new resolve endpoint — the endpoint would have duplicated the normalization round-trip the door already owns.
- The deliberate contract change: `ReadProjectManifest_NoExplicitProject_ResolvesThroughTheOnboardCwd` now pins `create=false` in the resolve body (was cwd-only).

## Deviations

- **T2 AC-3, left unticked honestly.** The letter said "NO test edits in this slice"; the slice carried one mechanical DI-wiring line in a test (constructor wiring for the new singleton). Review verdict recorded in the task log: intent of the AC (no behavioral/test-contract drift in the neutral slice) held; the tick was not forced. Reviewer judged it tick-worthy on intent — human may tick it at the review tap if agreed.

## For the human

1. The recorded deviation above (T2 AC-3 unticked per its letter) — the one judgment call in the wave.
2. Microbatch ledger items #318–#320 filed for the non-blocking findings from review.
3. Ships with the next release — no config or migration steps.

<refs>
- Spec: 29b2c8fe-bcb2-4ca8-904c-371a8fbc1288 (validated).
- Plan: 3e6b2cec-1e6b-4868-a844-a25367481f71 (validated), slices 01–04.
- Tasks: 09680522 (01), d6cc96c5 (02), fdb157d4 (03), 354eecc3 (04) — all done.
- Commits: af6d052 (host create flag), d39cb54 (ProjectResolver consolidation), 0db6dc9 (read-door flip + contract pins).
- Key files: src/Overmind.Host/Api/OnboardApi.cs, src/Overmind.Host/Api/Contracts/OnboardContracts.cs, src/Overmind.Mcp/ProjectResolver.cs, src/Overmind.Mcp/OvermindTools.cs, src/Overmind.Mcp/FeatureCodeTools.cs, src/Overmind.Mcp/OvermindNotificationTools.cs.
</refs>

<obstacles>
- One mid-flight upsert failure (API connection died during the task-batch write) — read-back showed an empty roster, batch resent cleanly, no duplicates.
- None otherwise; the wave landed as planned with the single recorded T2 deviation.
</obstacles>

<log>
- 2026-10-06 [agent] — result v1 drafted after the full wave: 4 tasks done, gate 2253/0, spec sweep 8/8; commits af6d052 / d39cb54 / 0db6dc9; one recorded deviation (T2 AC-3 unticked per letter, DI-wiring test edit); ledger #318–#320 filed; status draft, awaiting human validation tap.
</log>
