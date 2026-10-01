---
feature: 099
slug: agent-claims
status: validated
created: 2026-09-30
validated: 2026-09-30
---

# Plan 099 — Agent claims: current pin → in-work claims with alias+vendor ("Dan by qwen"), N agents per project

5 tasks, strictly sequential (shared files: `OvermindPlanningTools.cs`, `OvermindApiClient.cs`, `PlanningFeatureService.cs` chain). Backend first, web second, docs/closeout last.

## Key implementation decisions

- **Session transport = HTTP header.** The adapter stamps every API call with `X-Overmind-Session: <sessionId>` (one `DelegatingHandler`/header-site in the adapter's `HttpClient` setup, `Overmind.Mcp/OvermindApiClient.cs`). The host derives liveness from it: presence → update `AgentSession.LastSeenAt` (server-side touch, zero extra requests); absence → human/legacy caller → never locked. The session id never travels as a tool parameter.
- **Lock lives server-side**, in the planning service layer where feature mutations funnel (`PlanningFeatureService` / `PlanningPolicy`), not in the adapter. One check: does the operation mutate feature F (or its artifacts), is F claimed, does the claim's session match the caller header → else 409 naming the holder.
- **Derived claim state** (`alive|stale|ended`) computed on read: `EndedAt != null` → ended; `LastSeenAt` older than project `ClaimStaleThresholdMinutes` → stale; else alive. Threshold 0 disables stale (only ended counts).
- **Auto-adopt rule:** claim whose session is ended and whose `alias+vendor` equals the claiming caller's → re-bind to the new session id. Live holder with matching alias+vendor → 409 (collision protection).
- **`planning_set_current` is removed**, not deprecated: MCP tool registration, HTTP endpoint, web pin mutation — all go. `planning_current_task` is untouched.

## Task chain

1. **01 — Core model + migration.** `AgentSession` entity; `PlanningFeature` claim fields (drop `IsCurrent`); `Project` settings (`ClaimStaleThresholdMinutes` default 15, `AgentNamingRules`); claim/unclaim/take-over/adopt + derived state as service logic with session id as a plain parameter; EF migration. Full unit coverage of the rules matrix (409s, take-over, adopt, done-auto-release, stale derivation, threshold 0).
2. **02 — Session lifecycle + lock wiring.** Host: register/end session endpoints, header-touch, lock enforcement across the 12 feature-scoped mutations, auto-release on `set_feature_status(done)`, claim-409 on done features. Adapter: session register on start, header on every call, `EndedAt` on stdin-EOF shutdown (bounded ~3s timeout).
3. **03 — MCP tools + HTTP API surface.** `planning_claim_feature` / `planning_unclaim_feature`; delete `planning_set_current`; claim fields + derived state into `task_queue`, `list_features`, `read_feature`, board HTTP payload; naming-rules block in onboarding payload; claim/unclaim HTTP endpoints for the UI; `OnboardApi` + contracts + mapper updates.
4. **04 — Web UI.** Claim chip on the card («Dan by qwen» + age + alive dot / muted stale / ended ghost — 084 TTL pattern); filter «Current only» → «Pinned only» (`pinned=1`); overview panel claim/unclaim human-door actions replacing pin; types/api cleanup of `isCurrent`.
5. **05 — Docs wave + closeout.** `overmind-ceremony` skill pin→claim language; global `CLAUDE.md` rule; `SessionBootstrapPrompt`/server instructions/quickstart text; live dogfood of the full matrix on a scratch project; architect light review; result artifact.

## Verification strategy

- Unit: the full claim matrix (tasks 01–02) — the rules table from the spec, each row a test.
- Contract: MCP tool surface after 03 (claim/unclaim/409 texts, no `set_current`).
- Web: `npm run lint && npx tsc --noEmit` + build after 04.
- Live dogfood in 05: claim → second-session 409 → unclaim → take-over of stale → EOF auto-release.

<log>
- 2026-10-01 [agent] — plan drafted after spec validation; 5 sequential tasks, header-based session transport, server-side lock.
- 2026-09-30 [agent] — validated with human proof: notification b9048316-77f1-493b-a9ea-fac134edf3b5 replied 2026-09-30 22:02:47 UTC
</log>