---
feature: 099
slug: agent-claims
status: inProgress
priority: p1
version: 1
created: 2026-09-29
---

# Feature 099 — Agent claims: current pin → in-work claims with alias+vendor ("Dan by qwen"), N agents per project

<goal>
Replace the single global `current` pin with a claim model: N agents can work the same project in parallel, each claiming the feature they are working. A claim is visible as `{Alias} by {Vendor}` — e.g. «Dan by qwen», «Bobby by claude», «BigBlagBoi by Z.ai». Today's single pin forces parallel sessions (WSL + win-PC + field agents) to fight over one boolean.

Unit of claim: the FEATURE (orchestrator holds the feature and distributes tasks to its own coders) — confirmed with the user 2026-09-29; task-level claiming is out of scope unless the spec reopens it.

<requirements>
1. Claim state on PlanningFeature: ClaimedByAlias + ClaimedByVendor + ClaimedAt (nullable). Feature with a live claim renders as «in work» by that agent; the old single current pin migrates/retires — final shape decided at spec.
2. Agent door: `planning_claim_feature(slug, alias, vendor)` / `planning_unclaim_feature(slug)`. Claiming a claimed feature → 409 naming the holder. Alias+vendor travel as call parameters — the MCP server is stateless and shared by all agents, no server-side identity.
3. Agent naming: rules live in project/global settings; the onboarding payload surfaces them ("agent naming rules"). The agent either uses the prescribed alias or invents one following the rules (naming-helper skill exists for creative help). No uniqueness enforcement beyond alias+vendor discrimination — display-name collisions are a UX smell, not a hard error.
4. Board UI: claim chip on the card («Dan by qwen») + claim age (ghost control: a dead agent's claim is visible by age and removable via human door; no auto-expiry guessing, TTL indicator à la 084).
5. Ceremony/docs update: overmind-ceremony skill, CLAUDE.md «pin the feature» rule, quickstart — all move from pin language to claim language in the same wave.
</requirements>

<non-goals>
- Task-level claims (the orchestrator distributes tasks privately).
- Worktree/branch automation (063 owns that; claims on the board + worktrees on disk complement each other).
- Presence/heartbeat infrastructure — claim age indicator only, v1.
- Vendor autodetection — the agent passes its own vendor string.
</non-goals>

<refs>
- Supersedes the «pin current on start» rule in the crew memory/docs (pin-current-feature-on-start).
- 063-worktree-feature-binding — sibling; claims identify WHO, worktrees identify WHERE.
- 084 deadTarget TTL indicator — the ghost-age pattern to reuse.
</refs>

<log>
- 2026-09-30 [auto] — artifact created → inProgress
</log>
