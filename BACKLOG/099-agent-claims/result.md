---
feature: 099
slug: agent-claims
status: validated
created: 2026-10-02
validated: 2026-10-02
---

# Result 099 — Agent claims: current pin → in-work claims with alias+vendor ("Dan by qwen"), N agents per project

## Outcome

All five tasks landed. The global current pin is gone end-to-end: data model, MCP surface, HTTP doors, web UI, ceremony docs. A claim is a hard mutation lock bound to MCP session liveness — the board now shows who holds what, live/stale/ended at a glance, and the human door manages claims without session restrictions.

Per task:

- **01 Core model + migration** — AgentSession entity (alias/vendor ≤32, Touch/End, liveness derivation), PlanningFeature claim fields + Claim/Release/AppendLog (IsCurrent/SetCurrent removed), Project.ClaimStaleThresholdMinutes + AgentNamingRules, FeatureClaimService with the full claim matrix (done→409, live foreign→409 naming holder, live same-identity→409, stale/ended take-over logged, restart auto-adopt, holder done-close auto-release), migration AddAgentClaims.
- **02 Session lifecycle + lock wiring** — X-Overmind-Session stamped inside CreateHttpClient (covers both client call sites), session register/end endpoints, SessionTouchMiddleware (server-side LastSeenAt on every headered request), ClaimLockFilter as the single agent-door funnel over the 12 feature-scoped mutations, adapter start/EOF lifecycle with a 3s bounded shutdown end.
- **03 MCP tools + API surface** — planning_claim_feature / planning_unclaim_feature (identity cap enforced client-side), claim/unclaim HTTP doors for both agent (session-bound) and web human (session-free, Ui-sourced), claim fields + derived state in board/queue/read payloads, onboarding agentIdentity block (project naming rules or generic hint), set_current fully removed.
- **04 Web UI** — FeatureClaimChip (alias by vendor + age + liveness: alive dot / muted «stale Nm» / ghost «ended» / muted «human-held» for human claims), board Claim/Unclaim with pending-disable, overview-panel human door, Pinned only filter (pinned=1, predicate claimedByAlias ≠ null), zero isCurrent in web/src.
- **05 Docs wave + closeout** — this artifact; pin→claim language in the onboarding quickstart; live dogfood of the whole matrix on an isolated host (below); ceremony skill + CLAUDE.md diffs handed to the user.

## Authorized deviations

- **Lock lives in an HTTP endpoint filter, not the service layer** (task 02, review-santioned): the human door shares the planning services and must never lock — ClaimLockFilter at the agent-door boundary is one funnel that cannot leak to the human door by construction. Spec's human-door carve-out outranks the task constraint's letter.
- **Human door mints its own session row** (task 03): the data model requires ClaimSessionId; a human claim without an adapter session creates a fresh session (goes stale like any other, keeps the claim take-over-able) and logs with the Ui source.
- **ClaimedByHuman boolean** (task 04, user-approved deviation from the no-backend anti-scope): review 099/03 flagged that human-held claims must not render as a live agent. The flag is set by the human door, cleared on release, surfaced as claimedByHuman on every claim-bearing payload, and the web chip renders a muted «human-held · age» without the liveness dot. Migration AddClaimedByHuman — Down uses native DROP COLUMN (FTS-trigger preservation, same as AddAgentClaims).
- **Compile-driven pin removal pulled into task 01**: removing IsCurrent from the entity broke Host/Mcp compile, so the pin surface (POST current endpoints, planning_set_current tool, payload fields, pin tests) was removed there instead of tasks 02/03. Sequencing note: the wave must land as one commit/deploy — the board filter is dead in web between tasks 01 and 04.

## Spec decisions recorded mid-wave

- Agent-door import is claim-aware (user-approved in the task 02 fix round): import parses first, 409s FeatureClaimHeld when the imported slug is claimed by a foreign session, writes nothing in that case; unclaimed/own-claimed pass; headerless (human parity) unlocked.
- Human claim on a live agent claim is a take-over, never a 409 (review 099/03 blocker): isHuman branches before the holder gate in ClaimAsync.

## Verification evidence

Per-task gates at close:

- Task 01: build 0/0; suite 1719→1720 (fix round) green.
- Task 02: build 0/0; suite 1773 → fix round 1776 green.
- Task 03: build 0/0; suite 1791 green.
- Task 04: web lint 0 errors / tsc clean / build clean / tests 48-48; backend build 0/0, Core 252, Infra 552, Api 570 green; micro round → Infra 553.
- Task 05 final tree: dotnet build 0 warnings 0 errors; full suite **1794 passed / 0 failed** (Core 252, Infra 553, Api 570, Mcp 419); web lint 0 errors (6 pre-existing img warnings), tsc clean, build clean, tests 48/48.

Live dogfood (isolated scratch HOME, host on 127.0.0.1:4799, prod 4761 untouched, two real adapter processes over stdio JSON-RPC, transcript /tmp/dogfood-099-transcript.txt):

1. Adapter A (Dot by coder) claimed 099-agent-claims → claimState alive.
2. Adapter B (Bob by qwen) mutation → 409 «claim held by Dot by coder».
3. A unclaimed → released.
4. A re-claimed, session aged past the 15m threshold → B claim took over, feature log: «took over stale claim of Dot by coder».
6. Foreign import of B-claimed import-locked → 409 naming Bob by qwen; nothing written (title unchanged).
5. A terminated cleanly (EOF) → session EndedAt set, claim renders ended — released to take-over.

All six scenarios green.

## Follow-ups (microbatch queue)

- Barrel export ordering nits (index.ts) and a duplicated board-invalidation in use-planning — cosmetic.
- Allow-list filter test: assert every non-GET route under /api/agent/planning carries the lock filter (review 099/02 observation) — belt-and-suspenders for opt-in coverage.
- NormalizeIdentity duplicated between PlanningFeature and AgentSession — extract a shared normalizer.
- Same-session stale re-claim silently rewrites feature claim identity without touching the AgentSession row — decide whether identity drift is acceptable.
- Human-held claims age out after the project stale threshold of inactivity and become take-over-able by any session — spec-consistent, but consumers should know a board claim made by a human is not permanent (architect 099/05 N5).
- Crew memory tombstone: pin-current-feature-on-start memory file needs the post-deploy rewrite to claim language (agent-owned memory, not board).
- CS8629-class nullable followups: none open.

## Status

Feature ready for review; result stays draft until user validation (architect light 099/05: APPROVED-WITH-WARNINGS — W1 ceremony matrix cell and W2 reorder echo liveness closed in the micro round; N5 appended to follow-ups). Post-deploy: crew memory tombstone rewrite (above) — the wave itself must land as one deploy per the task 01 sequencing note.

<log>
- 2026-10-02 [agent] — drafted by dotnet-coder at the wave close: outcome per task, authorized deviations, spec decisions, verification evidence (1794/0 full suite + 6-scenario dogfood transcript), microbatch follow-ups. Draft pending architect-light + user validation.
- 2026-10-02 [agent] — architect 099/05 micro round: W1 ceremony matrix cell flipped to planning_claim_feature/planning_unclaim_feature (last pin leftover), W2 reorder echo wired through ClaimLivenessAsync (null⇒unclaimed invariant restored) pinned by Reorder_Echo_CarriesClaimFieldsForClaimedFeature, N5 human-claim aging appended to follow-ups.
- 2026-10-02 [agent] — Architect 099/05 micro round: W1 ceremony matrix cell → claim tools (last pin leftover), W2 reorder echo liveness via ClaimLivenessAsync + pinning test, N5 human-claim aging documented in follow-ups; status paragraph updated to APPROVED-WITH-WARNINGS with W1/W2 closed.
</log>