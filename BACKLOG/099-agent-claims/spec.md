---
feature: 099
slug: agent-claims
status: validated
created: 2026-09-30
validated: 2026-09-30
---

# Spec 099 — Agent claims: current pin → in-work claims with alias+vendor ("Dan by qwen"), N agents per project

## Goal

Replace the single global `current` pin with a claim model: N agents work the same project in parallel, each claiming the feature they are on. A claim renders as `{Alias} by {Vendor}` (e.g. «Dan by qwen») and — this is the load-bearing part — acts as a **mutation lock** backed by **MCP session liveness**: a claim dies when its session dies, so ghosts clear themselves and stale claims are take-over-able. No more parallel orches fighting over one boolean.

## Decisions (user-approved in chat, 2026-09-30)

- **Pin dies, no retirement path.** `planning_set_current` is removed from the agent door (tool gone, not deprecated-stub). `PlanningFeature.IsCurrent` is dropped via migration. `planning_current_task` is unaffected (slug-based). The board UI filter «Current only» becomes «Pinned only», filtering features with a claim (`claimedBy != null`). The crew habit "pin the feature on start" becomes "claim the feature on start".
- **Claim = hard mutation lock.** Non-holder sessions get 409 from feature-scoped mutations, error naming the holder. Reads stay open. Human door (web UI) is never locked.
- **Liveness = session lifecycle, not trust.** The adapter is a per-session stdio subprocess (`Overmind.Mcp/Program.cs` → `WithStdioServerTransport()`), one per Claude Code session, shared by the main agent and all its subagents. Binding claims to the adapter session means: subagents inherit holder rights automatically (they ride the same MCP connection), foreign sessions (other terminal, WSL, win-PC, field agent) are locked out. The session id is injected by the adapter — it is never an agent-supplied parameter, so one agent cannot impersonate another's session.
- **Stale threshold is a per-project setting.** Typed field on `Project` following the `PlanningMaterialization` pattern (`Project.cs:16-17`). Not global `AppSetting` (that is key/value global-only).
- **Graceful death auto-releases.** Client dies → kernel closes pipes → adapter sees EOF on stdin → shutdown hook marks the session ended (best-effort HTTP, timeout-bounded) → the claim releases itself immediately. Crash paths (machine/WSL down, adapter killed) leave no `EndedAt` — the stale threshold covers them.

## Data model

- `PlanningFeature`: drop `IsCurrent` + `SetCurrent`; add `ClaimedByAlias` (string?), `ClaimedByVendor` (string?), `ClaimedAt` (DateTime?), `ClaimSessionId` (Guid?).
- New entity `AgentSession`: `Id` (Guid), `Alias`, `Vendor`, `StartedAt`, `LastSeenAt`, `EndedAt` (DateTime?). Claimed values are non-empty, length-capped (alias ≤ 32, vendor ≤ 32); the pair `alias+vendor` is not unique — display collisions are a UX smell, not an error.
- `Project`: add `ClaimStaleThresholdMinutes` (int, default 15; 0 = stale detection off, only `EndedAt` counts) and `AgentNamingRules` (string?, null → onboarding shows a generic "invent an alias" hint instead of the block). Setter method follows `SetPlanningWorkflow` shape.
- One feature holds at most one claim (the fields above). One session may hold many claims (parallel features per agent are legal).

## Session lifecycle

- **Register:** adapter start → create `AgentSession` (HTTP, via the existing `OvermindApiClient` path).
- **Touch:** every MCP tool call through the adapter updates the session's `LastSeenAt`.
- **End:** stdin EOF / shutdown → set `EndedAt`. Best-effort, bounded timeout — a dead host API must not hang the shutdown.
- **Claim state derivation:** `EndedAt != null` → `ended`; else `LastSeenAt` older than the project threshold → `stale`; else `alive`.

## MCP agent door

- `planning_claim_feature(projectId, slug, alias, vendor)`:
  - feature `done` → 409.
  - live claim (`alive`) held by a different session → 409 naming the holder (`claim held by Dan by qwen (alive)`), regardless of alias+vendor match — name collisions cannot hijack a live holder.
  - `stale` or `ended` claim by a different session → take-over succeeds, logged: `Bobby by claude took over stale claim of Dan by qwen`.
  - claim whose session is `ended` and whose alias+vendor equals the caller's → **auto-adopt** (adapter restart case): seamless re-bind to the new session id, logged `session restarted, claim adopted`.
  - free feature → claim created, logged.
- `planning_unclaim_feature(projectId, slug)`: holder session only; any other session → 409 naming the holder. The human door unclaims anything.
- Feature-scoped mutations locked to the holder session (409 naming the holder, read-only for others): `planning_set_feature_status`, `planning_update_feature`, `planning_set_feature_priority`, `planning_move_feature`, `planning_add_feature_dependency`, `planning_remove_feature_dependency`, `planning_upsert_artifact`, `planning_upsert_tasks`, `planning_patch_artifact`, `planning_append_log`, `planning_set_status`, `planning_validate` — when they target the claimed feature or its artifacts. Unclaimed features behave exactly as today (no lock).
- Not locked (project-level, shared by design): `planning_materialize`, notification tools, entries, search.
- When the holder closes the feature `done`, the claim auto-releases.
- Claims do not drive the lifecycle status machine — a claimed draft feature still shows status `draft`; «in work» rendering is claim-derived presentation.

## Read-side surface

- Claim fields + derived state (`alias`, `vendor`, `claimedAt`, `alive|stale|ended`) join the feature payloads the board and queue already return: `planning_task_queue` entries, `planning_list_features`, board HTTP payload, `planning_read_feature`.
- Onboarding payload (`overmind_onboard`) surfaces an **agent naming rules** block: the project's `AgentNamingRules` if set, else the generic hint.

## UI (web)

- Card chip: «Dan by qwen» + claim age (from `ClaimedAt`) + liveness indicator (alive dot / muted «stale Nm» / ended ghost) — the 084 `deadTarget` TTL pattern.
- Filter chip «Current only» → «Pinned only»: filters features with any claim (URL param `current=1` → `pinned=1`, `board-filters.ts`, `use-board-filters.ts`, `board-filter-bar.tsx`).
- Human door: claim / unclaim / take-over any feature from the board UI, no session restrictions.
- Attention page (087) claim integration: out of scope v1.

## Migration

- One EF migration: drop `IsCurrent`, add claim columns + `AgentSessions` table + the two `Project` fields.
- No data migration for claims: the prod pin is currently unset. Ended sessions are kept (audit trail); the table is small by construction.

## Docs & ceremony wave (same wave, not a follow-up)

- `overmind-ceremony` skill: pin language → claim language (`planning_set_current` → `planning_claim_feature` in the ceremony loop, briefing contract untouched — subagents inherit).
- Global `CLAUDE.md` «Pin the feature you are working on» rule → claim wording.
- Onboarding quickstart text on the server (`options.ServerInstructions`, quickstart block).
- Crew memory file `pin-current-feature-on-start` gets tombstoned/rewritten after deploy (agent-owned memory, not board).

## Non-goals

- Task-level claims (orchestrator distributes tasks privately).
- Worktree/branch automation (063 owns that).
- Presence/heartbeat infrastructure beyond `LastSeenAt` touch.
- Vendor autodetection — the agent passes its own vendor string.
- Attention page claim integration.
- Locking project-level tools (`planning_materialize` et al).

<log>
- 2026-09-30 [agent] — spec drafted from Socratic gate: pin death + filter rename, hard mutation lock, session-liveness binding (stdio EOF → auto-release, stale threshold fallback, restart auto-adopt), stale threshold as per-project setting.
- 2026-09-30 [agent] — validated with human proof: notification 131bb170-5144-42b5-991d-d33ef5a4966a replied 2026-09-30 20:59:07 UTC
</log>