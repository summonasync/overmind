---
feature: 114
task: 02
slug: claims-onboarding-docs
status: done
review: light
created: 2026-10-04
brief: Kit lane: ceremony Claims section + orchestrator claim steps + crew-repo commit
---

# Task 02 — Crew kit: Claims workflow section + orchestrator claim steps

<goal>
The crew skills carry the same five-point practice, phrased for their readers: the ceremony loop and the orchestrator flow.
</goal>

<scope>
- src/OvermindCrewKit/.claude/skills/overmind-ceremony/SKILL.md: the existing single claim paragraph (the "Claim the feature you are working on" bullet in the Ceremony Loop rules) grows into a structured "Claims workflow" subsection: when (before set_current / first mutation), who (orchestrator claims; sub-agents inherit the lock through the shared adapter session — never claim/unclaim themselves), identity (alias+vendor from the agentIdentity block), 409 recipe (live holder → ask the user; stale/ended → take-over legitimate), EOF auto-release + unclaim at handoff/wave close.
- src/OvermindCrewKit/.claude/skills/feature-orchestrator/SKILL.md: the orchestration flow gains explicit steps — claim before dispatching sub-agents, unclaim when the wave closes (where the current steps say "Work the pinned current feature", claims replace the pin vocabulary).
- Checkpoint commit in the crew-kit repo itself (git -C src/OvermindCrewKit commit), message naming 114.
</scope>

<anti-scope>
- No other skills; no kit plumbing (116); no text copied verbatim from the quickstart — same five points, phrased for each skill's voice.
</anti-scope>

<acceptance-criteria>
- [x] overmind-ceremony has the Claims workflow section with all five practice points.
- [x] feature-orchestrator flow contains explicit claim/unclaim steps.
- [x] Crew-kit checkpoint commit landed, working tree clean.
</acceptance-criteria>

<refs>
- Spec 93282324; plan 5f55b79d; 099-agent-claims; OnboardQuickstart.cs (phrasing parity reference).
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof 301c2149); orchestrator-executed per plan.
- 2026-10-04 [agent] — [agent] taken by orchestrator (kit lane is orchestrator-executed per plan).
- 2026-10-04 [agent] — [agent] Kit lane done: ceremony gained the Claims workflow section (when/who/identity/409/EOF) + matrix updated (set_current retired, claim/unclaim + delete_artifact listed, date bumped 2026-10); feature-orchestrator picked queue-based flow + claim at takeover + Phase 2 sub-agent inheritance note + Wave Closeout unclaim. Found and fixed stale pin vocabulary that still taught the REMOVED planning_set_current — the exact "practice not landing" this feature hunts. Crew-kit checkpoint 788430e; note: kit working tree carries unrelated materialized BACKLOG drift (not mine, left untouched). Awaiting joint light review with task 01.
</log>