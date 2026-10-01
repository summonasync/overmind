---
feature: 099
task: 05
slug: agent-claims
status: todo
review: light
created: 2026-09-30
depends_on: 04
brief: ceremony skill + CLAUDE.md + server text pin→claim, live dogfood matrix, architect light, result artifact
---

# Task 05 — Docs wave + closeout

<goal>
The same-wave ceremony/doc flip from pin language to claim language, live verification of the whole matrix, review, and the result artifact.
</goal>

<scope>
- /home/johan-boolman/.claude/skills/overmind-ceremony/SKILL.md: pin → claim language (ceremony loop, briefing contract note that subagents inherit via the shared session).
- Global CLAUDE.md «Pin the feature you are working on» rule → claim wording (via the user-visible diff; flag the config-file edit for the user).
- src/Overmind.Mcp/SessionBootstrapPrompt.cs + ServerInstructions + quickstart text: claim wording, naming-rules mention.
- Live dogfood on a scratch project against a locally running host: claim → foreign-session mutation 409 → holder unclaim → stale take-over (short threshold) → EOF auto-release on adapter kill.
- architect-review light over the whole wave; fix blockers.
- Result artifact: outcome, deviations, follow-ups (crew memory tombstone of pin-current-feature-on-start noted as post-deploy agent work).
</scope>

<anti-scope>
No product code changes beyond what review forces. No migrations. No new features.
</anti-scope>

<constraints>
Docs in English; skill/CLAUDE.md edits are config-adjacent — surface diffs to the user rather than silently rewriting enforcement sections (CLAUDE.md enforcement/config sections are human-only; the pin rule line lives outside them — if in doubt, present the diff and ask).
</constraints>

<acceptance-criteria>
- [ ] Skill + CLAUDE.md + server text use claim language, no pin leftovers in ceremony flow
- [ ] Live dogfood matrix executed and logged: claim, foreign 409, unclaim, stale take-over, EOF auto-release
- [ ] Full dotnet suite + web lint/tsc/build green on the final tree
- [ ] architect light review done, blockers fixed or waived with rationale
- [ ] Result artifact written; feature ready for review
</acceptance-criteria>

<refs>
- Spec: 0e774399 (Docs & ceremony wave + Migration sections).
- Tasks 1–4 — the code this closeout verifies.
</refs>

<obstacles></obstacles>

<log></log>