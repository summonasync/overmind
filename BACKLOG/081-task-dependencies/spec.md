---
feature: 081
slug: task-dependencies
status: validated
created: 2026-09-27
validated: 2026-09-27
---

# Spec 081 — Task-level dependencies: blockedBy on task artifacts, honored by current_task (030-семантика на уровень тасок)

<goal>
Task sequencing becomes machine-gateable: a task can declare `blockedBy` siblings, `planning_current_task` skips blocked tasks in favour of the next actionable one (stop-reason names the chain when everything left is blocked), and listings answer "which todo task is unblocked right now?" without bodies. Extends the shipped 030 feature-dependency semantics one level down, per user decisions: machine edges via the upsert field, closure = done|review, full-stack surface including web.
</goal>

## Data model (Core)

- Link table `PlanningTaskDependency` (030/EntryTag idiom): `BlockedById` → task artifact, `BlockingId` → task artifact; both same feature (cross-feature edges are a non-goal). Unique pair; cascade with artifact delete.
- Edges are expressed as **order numbers of sibling tasks** at the API surface (user decision: field in upsert, no separate add/remove tools); the service resolves order → artifact id at upsert time. The textual `020/T3`-style references in `<refs>` stay a HUMAN notation — the machine never parses them into edges.
- Closed edge: blocking task status ∈ {done, review} (full 030 closure downward — user decision). A task is **blocked** while it has ≥1 unclosed edge; self-reference, unknown order, duplicate edge → 400; cycles → 400 naming the chain (030 DFS style).

## Surface

- `planning_upsert_artifact` (task) and `planning_upsert_tasks` items gain optional `blockedBy: [order, ...]` — sibling orders of the same feature. Re-upsert replaces the task's edge set (declarative; empty array clears). Existing upserts without the field keep current behavior.
- `planning_current_task`: skips blocked tasks; the response stays honest — a blocked-skipped task is never returned; when every remaining task is blocked → stop/empty reason naming the blocker chain (task order + feature slug), same authority rules as feature-blocked STOP.
- Roster (`planning_list_artifacts`) and full feature read: task items gain additive `blocked` (bool) and `blockedBy` (resolved sibling orders, sorted) — the 080 roster contract grows task-only fields the same way review/acTicked grew it. Existing pins updated (070/080 canned contracts), additive only.
- Materialized task files: `depends_on:` frontmatter key (030 precedent — blockers-first, byte-stable round-trip; import maps it back to edges, letter-suffixed orders included).
- Web (full-stack per user decision): blocked chips on task cards + a dependencies panel on the feature page (030 Dependencies panel pattern), rendering order + title of blockers and closed state.

## Behavior rules

- Ordering of `planning_current_task` resolution is unchanged otherwise (lowest order first); blocked tasks are transparent to the algorithm.
- Edge changes ride the artifact upsert path — logNote/Version rules of 083 apply unchanged; the feature feature log gains no entries for edge edits (they are artifact-level).
- Deleting a task (human door) cascades its edges (030 link idiom); no orphan edges.
- `030` feature-level edges and task-level edges coexist without naming collisions (different surfaces, different payloads).

## Non-goals

- Cross-feature task edges (feature-level 030 covers that layer).
- Separate add/remove dependency tools (user: field in upsert).
- Parsing `<refs>` prose into edges.
- Blocking on non-task artifacts (spec/plan/result can't be blockers).

<log>
- 2026-09-27 [agent] — spec drafted; user decisions recorded: edges = upsert field (order-based), closure = done|review, full-stack including web. Human refs-notation explicitly out of machine scope.
- 2026-09-27 [agent] — validated with human proof: notification 9343e141-3580-4d2d-b4dd-4968818162dd replied 2026-09-27 18:45:48 UTC
</log>