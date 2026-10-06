---
feature: 047
slug: api-read-surface-polish
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 047 — API read-surface polish: per-status breakdown в list_features + три докгэпа описаний тулов

## Goal

Four cheap read-surface fixes, each of which cost the caller an extra call or a guess in the field. The main item makes a feature row on `planning_list_features` / `planning_task_queue` self-sufficient: today it carries only `taskTotal`/`taskDone`, so «4/7» is indistinguishable between «4 done, 3 untouched» and «4 done, 2 in progress, 1 blocked» — blocked and unplanned need opposite actions. The other three are tool-description gaps that force the caller to guess; two of them are residuals after 070, one is already resolved elsewhere and gets a tombstone here.

## Recon summary (code facts this spec is built on)

- Both read doors are pass-through: `planning_list_features` (OvermindPlanningTools.cs:96) and `planning_task_queue` (:500) return the Host API's JSON verbatim (`_api.GetAsync` → `JsonElement`). Adding additive fields to the Host DTOs automatically flows to both MCP tools; only their descriptions need touching.
- Host projections: `PlanningFeatureBoardItem` / `PlanningQueueItem` records in `src/Overmind.Host/Api/Contracts/PlanningContracts.cs`, mapped in `src/Overmind.Host/Api/Mapping/PlanningMapper.cs` (`ToBoardItem` / `ToQueueItem`), served by `PlanningFeaturesApi.cs:48` (board) and `PlanningQueueApi.cs` (queue list :40, single re-projection :84, cross-project list :126 — all one DTO).
- Task statuses are 5: `todo, inProgress, review, done, blocked` (`PlanningArtifactStatus`); fresh tasks start `Todo` (`PlanningArtifact.cs:96`), `draft`/`validated` are document-only in practice. A per-status map over all 5 keys therefore always sums to `taskTotal`.
- The web board shares the board DTO: `web/src/features/planning/types.ts` `PlanningFeatureBoardItem` mirrors the C# record field-for-field (`listPlanningBoard` → `GET /planning/features`). It does not call the queue endpoint.
- Description pins live in `tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs` (substring assertions on tool descriptions); canned Host responses live in `tests/Overmind.Mcp.Tests/Canned.cs`; Api-side mirrors of the board/queue DTOs live as local records in `PlanningApiTests.cs` / `PlanningQueueApiTests.cs`.

## Item 1 (main) — per-status task counts on both read doors

### Shape decision (body left it partially open — FLAGGED for the human)

The feature body prescribes a named per-status object and lists `{done, inProgress, todo, blocked}` — 4 keys. Tasks legally carry a 5th status (`review`), and this project's own flow leaves tasks in review routinely. A 4-key map would leave the same blindness the item complains about, one bucket over, and the counts would not sum to `taskTotal` for features with tasks in review.

**Proposed shape: `taskCounts` object with all 5 keys — `{todo, inProgress, review, done, blocked}`, invariant: sum(taskCounts) == taskTotal.**

This is the minimal additive shape consistent with the body (named object, same field family as `taskTotal`/`taskDone`); the only deviation from the body's literal list is the added `review` key. FLAG: the human trims `review` out at spec tap if 4 keys are truly wanted — the implementation is one dictionary away either way.

### Contract

- New record `PlanningTaskCounts(int Todo, int InProgress, int Review, int Done, int Blocked)` in `PlanningContracts.cs`, JSON camelCase: `"taskCounts": {"todo": 3, "inProgress": 2, "review": 1, "done": 4, "blocked": 1}`.
- `PlanningFeatureBoardItem` gains `PlanningTaskCounts TaskCounts` immediately after `TaskDone`.
- `PlanningQueueItem` gains `PlanningTaskCounts TaskCounts` immediately after `TaskTotal` — both projections stay consistent, as the body requires («в той же строке фичи» on both doors).
- `PlanningMapper` gains a `TaskCounts(artifacts)` helper (count tasks by status, kind == Task only); `ToBoardItem` and `ToQueueItem` both call it. Features with zero tasks get an all-zeros object (never null).
- Additive only: `taskTotal`/`taskDone` stay exactly where they are with unchanged semantics. Every existing consumer keeps working.

### Consumers

- MCP `planning_list_features` and `planning_task_queue`: automatic via pass-through; tool descriptions gain a field mention so agents learn the field exists.
- Web board: `types.ts` interface gains the field (contract mirror); rendering the breakdown in the UI is a NON-GOAL (the body scopes this to the MCP read surface; UI can ride a later polish batch).
- The single-feature queue re-projection (`PlanningQueueApi.cs:84`) and the cross-project issues board share the DTO and get the field for free.

## Item 2 — «Markdown body with its XML sections» phrase (residual after 070)

070 already landed the per-kind section list in the tool-level description (`Expected XML sections — task: goal, scope, anti-scope, constraints, acceptance-criteria, refs, obstacles, log; spec/plan/result: free-form markdown with <log> as the only machine-parsed section`). The residual is the `body` PARAMETER description (`OvermindPlanningTools.cs:246`), which still reads «Markdown body of the artifact with its XML sections.» — the exact required-looking phrase the field report flagged, one line away from the list that answers it.

**Fix:** replace the parameter text with a pointer, no duplicate list, no example needed:

> `Markdown body. Expected XML sections per kind are listed in this tool's description; plain markdown is accepted — for spec/plan/result only the <log> section is machine-parsed.`

The original feedback offered «document with an example or remove as stale». The section list already exists one line up (that IS the documentation), so the pointer variant is the minimal honest fix; the stale implication of a required format goes away.

## Item 3 — `overmind://resources` pointer — REMOVED (already resolved)

<removed 2026-10-06 — superseded by 070 (R5): both dead-pointer sites were removed and absence-pinned; current ServerInstructions (Overmind.Mcp/Program.cs) name `list_entries` for gates/guards/notes/decisions and never mention overmind://resources. Confirmed live 2026-10-06; 090 spec R3 carries the same tombstone.>

No code. This tombstone is the record the feature body asked for. FLAG: the feature body predates the 070 fix; the human sees this at tap time and can drop the item from the feature body at will.

## Item 4 — `order` semantics: holes are facts (residual after 070)

070 landed «Order is a sort key within the feature — lowest order first…» in both `planning_upsert_artifact` and `planning_upsert_tasks`. The field report's actual question — sort (holes = dirt) or identifier (holes = fact)? — is still unanswered, and since 081/090 the answer is «both»: `blockedBy` edges and `{featureSlug}/T{order}` aliases address tasks BY order, so renumbering to close a hole would silently break references.

**Fix:** one appended sentence in the order-semantics block of BOTH descriptions, plus a short note on the `order` parameter:

> `Gaps in order numbering are legal and meaningful — imported backlogs skip tasks that were planned but never written; order doubles as the stable handle (blockedBy edges and {featureSlug}/T{order} aliases address tasks by order), so never renumber existing tasks to close a hole.`

Parameter (`order`, upsert): `Sort key and stable handle; gaps are legal — do not renumber to close them.`

## Acceptance criteria

- [x] `taskCounts` with 5 keys present on both `PlanningFeatureBoardItem` and `PlanningQueueItem`; sum always equals `taskTotal`; zero-task features return all-zeros, never null.
- [x] Board endpoint, queue endpoint, single-queue re-projection and MCP `planning_list_features` / `planning_task_queue` all carry `taskCounts` (pass-through asserted).
- [x] `planning_list_features` and `planning_task_queue` descriptions mention `taskCounts` (pinned).
- [x] `body` parameter of `planning_upsert_artifact` no longer reads «with its XML sections»; the pointer text is pinned.
- [x] Holes-are-legal sentence present in `planning_upsert_artifact` and `planning_upsert_tasks` descriptions + the `order` parameter note (pinned).
- [x] `web/src/features/planning/types.ts` mirror carries `taskCounts`; `npm run lint && npx tsc --noEmit` green.
- [x] No breaking change: all additions are additive; `taskTotal`/`taskDone` semantics and positions unchanged.
- [x] `dotnet build Overmind.sln` + full `dotnet test Overmind.sln` green.

## Non-goals

- Pagination/limits for `list_entries` — owned by 041-entries-metadata-listing.
- Status/transition behavior changes — owned by 043.
- Rendering `taskCounts` in the web board UI (contract mirror only).
- Exposing resources as a tool (item 3's alternative) — superseded by 070; resources stay resource-only.
- Changing upsert/order BEHAVIOR — description text only; gaps already work.

## Refs

- FEATURELIST/09-api-nits.md — main document (breakdown + order-hole semantics).
- FEATURELIST/03-tool-description-gaps.md — XML-sections phrase + resources pointer (original report).
- 070-response-polish-round3 (R5, task 03) — supersedes item 3; landed the section list and the first order sentence.
- 090-field-nits-batch (R3 tombstone) — sibling doc-gap batch, same supersession record.
- 081 (task dependencies, blockedBy by order) + 090 (T{order} aliases) — why order is also a stable handle, not just a sort key.

<log>
- 2026-10-06 [agent] — spec drafted from feature body + FEATURELIST 09/03 + code recon (PlanningContracts, PlanningMapper, PlanningFeaturesApi, PlanningQueueApi, OvermindPlanningTools descriptions, Program.cs instructions, Canned.cs, contract-pin tests, web types.ts). Shape fork (4 vs 5 keys) resolved to 5 keys with the invariant sum == taskTotal; flagged for human. Item 3 marked superseded by 070 after verifying the dead pointer is gone and absence-pinned.
- 2026-10-06 [agent] — validated with human proof: notification 25b83593-6332-4841-a85d-7b21bfe9e9b8 replied 2026-10-06 13:05:12 UTC
- 2026-10-06 [agent] — 047/T4 gate round: all 8 acceptance criteria ticked after green gate on 3aca50c — build 0w/0e; serial tests 2246/2246 (Core 367, Infra 692, Api 674, Mcp 513); web lint 0 errors + tsc clean + vitest 263/263 + next build green; consistency sweep clean (shape agreement Host DTO / Canned / web mirror, three doc-gap texts verbatim + pinned, pass-through asserted). Full evidence in task 03 log. Body change is checkbox ticks only — no semantic edits.
- 2026-10-06 [auto] — artifact demoted: validated → draft
- 2026-10-06 [agent] — validated with human proof: notification 447bcc54-9166-4450-a08a-f2809003db8e replied 2026-10-06 17:30:36 UTC
</log>